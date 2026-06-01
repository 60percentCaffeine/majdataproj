using UiPrototypeTemplateMod.Core;
using Xunit;

namespace UiPrototypeTemplateMod.Core.Tests
{
    public sealed class PrototypeVariantRouterTests
    {
        [Fact]
        public void RouterDefinesExactlyThreeInitialVariants()
        {
            Assert.Equal(3, PrototypeVariantRouter.VariantCount);
        }

        [Fact]
        public void NextAndPreviousCycleAcrossThreeVariants()
        {
            Assert.Equal(1, PrototypeVariantRouter.Next(0));
            Assert.Equal(2, PrototypeVariantRouter.Next(1));
            Assert.Equal(0, PrototypeVariantRouter.Next(2));

            Assert.Equal(2, PrototypeVariantRouter.Previous(0));
            Assert.Equal(1, PrototypeVariantRouter.Previous(2));
        }

        [Fact]
        public void VariantSwitchingDoesNotMutatePrototypeSession()
        {
            PrototypeSession session = PrototypeSession.CreateDefault();
            session.Apply(PrototypeAction.SongNext);
            session.Apply(PrototypeAction.Ok);
            session.Apply(PrototypeAction.DifficultyUp);

            int variant = PrototypeVariantRouter.Next(0);

            Assert.Equal(1, variant);
            Assert.Equal(PrototypePhase.DifficultySelect, session.Phase);
            Assert.Equal(1, session.SelectedSongIndex);
            Assert.Equal("Advanced", session.SelectedDifficulty.Name);
        }
    }
}
