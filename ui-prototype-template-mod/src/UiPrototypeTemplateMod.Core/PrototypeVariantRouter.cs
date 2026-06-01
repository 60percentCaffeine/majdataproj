namespace UiPrototypeTemplateMod.Core
{
    public static class PrototypeVariantRouter
    {
        public const int VariantCount = 3;

        public static int Next(int currentIndex)
        {
            return Wrap(currentIndex + 1);
        }

        public static int Previous(int currentIndex)
        {
            return Wrap(currentIndex - 1);
        }

        private static int Wrap(int index)
        {
            if (index < 0)
            {
                return VariantCount - 1;
            }

            if (index >= VariantCount)
            {
                return 0;
            }

            return index;
        }
    }
}
