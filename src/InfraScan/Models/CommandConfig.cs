using System;

namespace InfraScan.Models
{
    public enum CommandCategory
    {
        Table,   // Commands whose output feeds the summary table
        Output   // Commands whose output is shown as terminal screenshots
    }

    public class CommandConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public CommandCategory Category { get; set; } = CommandCategory.Table;
        public bool IsDefault { get; set; } = false;
        public int Order { get; set; } = 0;

        public CommandConfig() { }

        public CommandConfig(string name, string command, CommandCategory category, bool isDefault, int order)
        {
            Name = name;
            Command = command;
            Category = category;
            IsDefault = isDefault;
            Order = order;
        }
    }
}
