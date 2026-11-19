using System;

namespace ChipOSMarkII
{
    /// <summary>
    /// Small local task item used by the editor-playable ChipOS shell.
    /// </summary>
    [Serializable]
    public sealed class ChipTaskItem
    {
        public string title;
        public string description;

        public ChipTaskItem(string title, string description)
        {
            this.title = title;
            this.description = description;
        }
    }
}
