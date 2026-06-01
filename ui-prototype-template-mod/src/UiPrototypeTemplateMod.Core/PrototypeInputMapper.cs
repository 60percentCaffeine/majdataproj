using System.Collections.Generic;

namespace UiPrototypeTemplateMod.Core
{
    public static class PrototypeInputMapper
    {
        public static PrototypeInputFrame Map(PrototypePhase phase, RawPrototypeInput input)
        {
            List<PrototypeAction> actions = new List<PrototypeAction>(4);

            if (input == null)
            {
                return new PrototypeInputFrame(PrototypeInputSource.None, actions);
            }

            switch (phase)
            {
                case PrototypePhase.SongSelect:
                    if (input.A3)
                    {
                        actions.Add(PrototypeAction.SongNext);
                    }

                    if (input.A6)
                    {
                        actions.Add(PrototypeAction.SongPrevious);
                    }

                    AddOkBack(input, actions);
                    break;
                case PrototypePhase.DifficultySelect:
                    if (input.A3)
                    {
                        actions.Add(PrototypeAction.DifficultyUp);
                    }

                    if (input.A6)
                    {
                        actions.Add(PrototypeAction.DifficultyDown);
                    }

                    AddOkBack(input, actions);
                    break;
                case PrototypePhase.Confirmed:
                    if (input.A5)
                    {
                        actions.Add(PrototypeAction.Back);
                    }

                    break;
            }

            return new PrototypeInputFrame(input.Source, actions);
        }

        private static void AddOkBack(RawPrototypeInput input, List<PrototypeAction> actions)
        {
            if (input.A4)
            {
                actions.Add(PrototypeAction.Ok);
            }

            if (input.A5)
            {
                actions.Add(PrototypeAction.Back);
            }
        }
    }
}
