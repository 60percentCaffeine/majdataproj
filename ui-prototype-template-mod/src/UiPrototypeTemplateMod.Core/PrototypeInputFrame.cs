using System.Collections.Generic;

namespace UiPrototypeTemplateMod.Core
{
    public sealed class PrototypeInputFrame
    {
        public PrototypeInputFrame(PrototypeInputSource source, IReadOnlyList<PrototypeAction> actions)
        {
            Source = source;
            Actions = actions;
        }

        public PrototypeInputSource Source { get; }
        public IReadOnlyList<PrototypeAction> Actions { get; }

        public bool HasActions
        {
            get { return Actions.Count > 0; }
        }
    }
}
