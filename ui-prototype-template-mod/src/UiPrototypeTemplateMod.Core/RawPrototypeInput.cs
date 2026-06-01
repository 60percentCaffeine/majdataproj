namespace UiPrototypeTemplateMod.Core
{
    public sealed class RawPrototypeInput
    {
        public RawPrototypeInput(PrototypeInputSource source, bool a3, bool a4, bool a5, bool a6)
        {
            Source = source;
            A3 = a3;
            A4 = a4;
            A5 = a5;
            A6 = a6;
        }

        public PrototypeInputSource Source { get; }
        public bool A3 { get; }
        public bool A4 { get; }
        public bool A5 { get; }
        public bool A6 { get; }
    }
}
