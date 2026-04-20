using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace InfraScan.Services
{
    /// <summary>
    /// Captures real screenshots from Cockpit web interface using Playwright.
    /// Automates login, navigates to /system and /metrics, and takes full-page
    /// screenshots of the performance graphs.
    /// Falls back to placeholder images if Playwright browsers are not installed.
    /// </summary>
    public class CockpitScreenshotService
    {
        /// <summary>
        /// Event fired for each step of the Cockpit capture process,
        /// allowing the UI to display progress in the console.
        /// </summary>
        public event Action<string>? OnLog;

        private void Log(string message) => OnLog?.Invoke(message);

        /// <summary>
        /// Captures real screenshots of Cockpit's /system and /metrics pages.
        /// </summary>
        /// <param name="url">Base Cockpit URL, e.g. https://server:9090</param>
        /// <param name="username">Cockpit login username</param>
        /// <param name="password">Cockpit login password (plain text)</param>
        /// <returns>Tuple of (overview screenshot bytes, metrics screenshot bytes)</returns>
        public async Task<(byte[]? overview, byte[]? metrics)> CaptureScreenshotsAsync(
            string url, string username, string password)
        {
            IPlaywright? playwright = null;
            IBrowser? browser = null;

            try
            {
                // ── Step 0: Ensure Chromium is installed (auto-install on first use) ──
                await EnsureChromiumInstalledAsync();

                // ── Step 1: Initialize Playwright ──
                Log("🌐 Cockpit » Inicializando Playwright...");
                playwright = await Playwright.CreateAsync();

                // ── Step 2: Launch Chromium headless ──
                Log("🌐 Cockpit » Lanzando navegador Chromium (headless)...");
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

                // Create context with HTTPS error bypass (self-signed certs on :9090)
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();

                // ── Step 3: Navigate to Cockpit login ──
                Log($"🌐 Cockpit » Navegando a {url}...");
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 30000
                });

                Log("🌐 Cockpit » Página de login cargada correctamente");

                // ── Step 4: Perform login ──
                Log("🔑 Cockpit » Ingresando credenciales de usuario...");

                // Cockpit standard selectors
                await page.WaitForSelectorAsync("#login-user-input", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 15000
                });

                await page.FillAsync("#login-user-input", username);
                await page.FillAsync("#login-password-input", password);

                Log("🔑 Cockpit » Haciendo clic en 'Iniciar sesión'...");
                await page.ClickAsync("#login-button");

                // Wait for successful login — Cockpit redirects to the main dashboard
                // We wait for the navigation bar or system page to appear
                Log("🔑 Cockpit » Esperando autenticación...");
                try
                {
                    // Wait for the Cockpit shell/frame to load after login
                    await page.WaitForURLAsync($"**/*", new PageWaitForURLOptions
                    {
                        Timeout = 20000
                    });

                    // Give extra time for the dashboard framework to initialize
                    await page.WaitForTimeoutAsync(3000);

                    // Check if we're still on the login page (login failed)
                    var loginError = await page.QuerySelectorAsync("#login-error-message");
                    if (loginError != null)
                    {
                        var errorVisible = await loginError.IsVisibleAsync();
                        if (errorVisible)
                        {
                            var errorText = await loginError.TextContentAsync();
                            Log($"❌ Cockpit » Error de autenticación: {errorText}");
                            Log("⚠️ Cockpit » Usando imágenes placeholder como fallback...");
                            return GeneratePlaceholders(url, $"Error de autenticación: {errorText}");
                        }
                    }
                }
                catch (TimeoutException)
                {
                    Log("⚠️ Cockpit » Timeout esperando autenticación, intentando continuar...");
                }

                Log("✅ Cockpit » Login exitoso — sesión iniciada");

                // ── Step 5: Navigate to /system and take screenshot ──
                byte[]? overviewBytes = null;
                byte[]? metricsBytes = null;

                var systemUrl = url.TrimEnd('/') + "/system";
                Log($"📸 Cockpit » Navegando a {systemUrl}...");

                try
                {
                    await page.GotoAsync(systemUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 30000
                    });

                    // Wait for the system page content to render
                    Log("📸 Cockpit » Esperando renderizado de gráficos del sistema...");
                    await page.WaitForTimeoutAsync(5000);

                    // Try to wait for specific Cockpit system page elements
                    try
                    {
                        await page.WaitForSelectorAsync(".ct-overview, #overview, .pf-v5-l-gallery, .pf-c-page__main-section",
                            new PageWaitForSelectorOptions { Timeout = 10000 });
                    }
                    catch { /* Continue anyway — page structure may vary */ }

                    // Additional wait for graphs/charts to finish rendering
                    await page.WaitForTimeoutAsync(3000);

                    overviewBytes = await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        FullPage = true,
                        Type = ScreenshotType.Png
                    });

                    Log($"✅ Cockpit » Screenshot de /system capturado ({overviewBytes.Length / 1024} KB)");
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Cockpit » Error capturando /system: {ex.Message}");
                }

                // ── Step 6: Navigate to /metrics and take screenshot ──
                var metricsUrl = url.TrimEnd('/') + "/metrics";
                Log($"📸 Cockpit » Navegando a {metricsUrl}...");

                try
                {
                    await page.GotoAsync(metricsUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 30000
                    });

                    Log("📸 Cockpit » Esperando renderizado de gráficos de métricas...");
                    await page.WaitForTimeoutAsync(5000);

                    // Try to wait for metrics-specific elements
                    try
                    {
                        await page.WaitForSelectorAsync(".metrics-minute, .pf-v5-l-gallery, .ct-plot-group, .pf-c-page__main-section",
                            new PageWaitForSelectorOptions { Timeout = 10000 });
                    }
                    catch { /* Continue anyway */ }

                    // Additional wait for metric charts to finish rendering
                    await page.WaitForTimeoutAsync(3000);

                    metricsBytes = await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        FullPage = true,
                        Type = ScreenshotType.Png
                    });

                    Log($"✅ Cockpit » Screenshot de /metrics capturado ({metricsBytes.Length / 1024} KB)");
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Cockpit » Error capturando /metrics: {ex.Message}");
                }

                Log("🏁 Cockpit » Proceso de capturas completado");
                return (overviewBytes, metricsBytes);
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
            {
                Log("⚠️ Cockpit » Navegadores de Playwright no instalados");
                Log("   Ejecute: pwsh bin\\Debug\\net8.0-windows\\playwright.ps1 install chromium");
                Log("⚠️ Cockpit » Usando imágenes placeholder como fallback...");
                return GeneratePlaceholders(url, "Playwright browsers no instalados");
            }
            catch (Exception ex)
            {
                Log($"❌ Cockpit » Error inesperado: {ex.Message}");
                Debug.WriteLine($"Cockpit screenshot error: {ex}");
                return GeneratePlaceholders(url, ex.Message);
            }
            finally
            {
                // ── Cleanup ──
                try
                {
                    if (browser != null)
                    {
                        await browser.CloseAsync();
                        Log("🌐 Cockpit » Navegador cerrado");
                    }
                    playwright?.Dispose();
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
        /// <summary>
        /// Checks if Playwright's Chromium browser is installed locally.
        /// If not found, automatically downloads and installs it.
        /// This makes the app self-contained — no manual setup needed on new machines.
        /// </summary>
        private async Task EnsureChromiumInstalledAsync()
        {
            // Playwright stores browsers in %LOCALAPPDATA%\ms-playwright
            var playwrightPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ms-playwright");

            bool chromiumExists = false;
            if (Directory.Exists(playwrightPath))
            {
                // Check if any chromium directory exists
                var dirs = Directory.GetDirectories(playwrightPath, "chromium*");
                chromiumExists = dirs.Length > 0;
            }

            if (!chromiumExists)
            {
                Log("📥 Cockpit » Chromium no encontrado — instalando automáticamente...");
                Log("📥 Cockpit » Descargando Chromium (~89 MB). Esto solo ocurre la primera vez...");

                // Run the Playwright CLI install command programmatically
                var exitCode = await Task.Run(() =>
                    Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }));

                if (exitCode == 0)
                {
                    Log("✅ Cockpit » Chromium instalado correctamente");
                }
                else
                {
                    Log($"⚠️ Cockpit » La instalación de Chromium terminó con código: {exitCode}");
                }
            }
        }

        /// <summary>
        /// Generates placeholder images when Playwright capture fails.
        /// Uses TerminalImageRenderer for consistent styling with the rest of the report.
        /// </summary>
        private (byte[]? overview, byte[]? metrics) GeneratePlaceholders(string url, string reason)
        {
            var overview = TerminalImageRenderer.RenderTerminalOutput(
                $"Cockpit Web Interface\n" +
                $"URL: {url}\n" +
                $"Estado: No se pudo capturar ⚠️\n" +
                $"Razón: {reason}\n" +
                $"Nota: Para capturas reales, instale\n" +
                $"los browsers de Playwright.",
                "Cockpit - Vista General (Placeholder)");

            var metrics = TerminalImageRenderer.RenderTerminalOutput(
                $"Cockpit Metrics\n" +
                $"URL: {url}\n" +
                $"Estado: No se pudo capturar ⚠️\n" +
                $"Razón: {reason}\n" +
                $"Nota: Para capturas reales, instale\n" +
                $"los browsers de Playwright.",
                "Cockpit - Métricas (Placeholder)");

            return (overview, metrics);
        }
    }
}
