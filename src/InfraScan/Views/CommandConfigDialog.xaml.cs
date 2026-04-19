using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using InfraScan.Models;

namespace InfraScan.Views
{
    [SupportedOSPlatform("windows")]
    public partial class CommandConfigDialog : Window
    {
        public List<CommandConfig> Commands { get; private set; }

        public CommandConfigDialog(List<CommandConfig> commands)
        {
            InitializeComponent();
            Commands = commands.Select(c => new CommandConfig
            {
                Id = c.Id, Name = c.Name, Command = c.Command,
                Category = c.Category, IsDefault = c.IsDefault, Order = c.Order
            }).ToList();

            RefreshLists();
        }

        private void RefreshLists()
        {
            TableCommandsList.ItemsSource = null;
            TableCommandsList.ItemsSource = Commands.Where(c => c.Category == CommandCategory.Table).OrderBy(c => c.Order).ToList();

            OutputCommandsList.ItemsSource = null;
            OutputCommandsList.ItemsSource = Commands.Where(c => c.Category == CommandCategory.Output).OrderBy(c => c.Order).ToList();
        }

        private void EditCommand(string? id)
        {
            var cmd = Commands.FirstOrDefault(c => c.Id == id);
            if (cmd == null) return;

            var (name, command) = ShowCommandEditor(cmd.Name, cmd.Command, cmd.IsDefault);
            if (name != null)
            {
                cmd.Name = name;
                cmd.Command = command!;
                RefreshLists();
            }
        }

        private void AddCommand(CommandCategory category)
        {
            var (name, command) = ShowCommandEditor("", "", false);
            if (name != null)
            {
                int maxOrder = Commands.Where(c => c.Category == category).Select(c => c.Order).DefaultIfEmpty(0).Max();
                Commands.Add(new CommandConfig(name, command!, category, false, maxOrder + 1));
                RefreshLists();
            }
        }

        private (string? name, string? command) ShowCommandEditor(string name, string command, bool isDefault)
        {
            var dlg = new Window
            {
                Title = "Editar Comando",
                Width = 500, SizeToContent = SizeToContent.Height, MinHeight = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = FindResource("BgDarkBrush") as System.Windows.Media.Brush,
                ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize,
                Padding = new Thickness(0, 0, 0, 16)
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            var lblName = new TextBlock { Text = "Nombre para el informe", Style = FindResource("FieldLabel") as Style };
            var txtName = new TextBox { Text = name, Style = FindResource("ModernTextBox") as Style, Margin = new Thickness(0, 0, 0, 12) };
            if (isDefault) txtName.IsEnabled = false;

            var lblCmd = new TextBlock { Text = "Comando Linux", Style = FindResource("FieldLabel") as Style };
            var txtCmd = new TextBox { Text = command, Style = FindResource("ModernTextBox") as Style,
                TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 80, Margin = new Thickness(0, 0, 0, 16) };

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnCancel = new Button { Content = "Cancelar", Style = FindResource("SecondaryButton") as Style, Margin = new Thickness(0, 0, 8, 0) };
            var btnOk = new Button { Content = "Aceptar", Style = FindResource("PrimaryButton") as Style };

            btnCancel.Click += (s, e) => dlg.DialogResult = false;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtCmd.Text))
                {
                    MessageBox.Show("Nombre y comando son requeridos.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                dlg.DialogResult = true;
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);

            stack.Children.Add(lblName);
            stack.Children.Add(txtName);
            stack.Children.Add(lblCmd);
            stack.Children.Add(txtCmd);
            stack.Children.Add(btnPanel);

            dlg.Content = stack;

            if (dlg.ShowDialog() == true)
                return (txtName.Text.Trim(), txtCmd.Text.Trim());
            return (null, null);
        }

        // Event handlers
        private void EditTableCmd_Click(object sender, RoutedEventArgs e) =>
            EditCommand((sender as Button)?.Tag?.ToString());

        private void EditOutputCmd_Click(object sender, RoutedEventArgs e) =>
            EditCommand((sender as Button)?.Tag?.ToString());

        private void AddTableCmd_Click(object sender, RoutedEventArgs e) =>
            AddCommand(CommandCategory.Table);

        private void AddOutputCmd_Click(object sender, RoutedEventArgs e) =>
            AddCommand(CommandCategory.Output);

        private void DeleteCmd_Click(object sender, RoutedEventArgs e)
        {
            var id = (sender as Button)?.Tag?.ToString();
            var cmd = Commands.FirstOrDefault(c => c.Id == id);
            if (cmd == null) return;

            string msg = $"¿Eliminar el comando '{cmd.Name}'?";
            if (cmd.Category == CommandCategory.Table && cmd.IsDefault)
            {
                msg += "\n\n⚠️ PRECAUCIÓN: Éste es un comando principal del sistema. Si lo elimina, la tabla del informe generado podría quedar con datos faltantes. ¿Desea continuar?";
            }

            if (MessageBox.Show(msg, "Confirmar Eliminación",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Commands.Remove(cmd);
                RefreshLists();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
