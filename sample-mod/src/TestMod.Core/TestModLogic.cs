namespace TestMod.Core
{
    public sealed class TestModLogic
    {
        public string StartupMessage()
        {
            return "Loaded";
        }

        public int AddScore(int currentScore, int delta)
        {
            if (delta < 0)
            {
                return currentScore;
            }

            return currentScore + delta;
        }
    }
}
