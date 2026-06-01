using System.Linq;
using UiPrototypeTemplateMod.Core;
using Xunit;

namespace UiPrototypeTemplateMod.Core.Tests
{
    public sealed class PrototypeInputMapperTests
    {
        [Fact]
        public void SongSelectMapsA3A6A4A5ToSongActions()
        {
            RawPrototypeInput input = new RawPrototypeInput(PrototypeInputSource.MajdataReflection, true, true, true, true);

            PrototypeInputFrame frame = PrototypeInputMapper.Map(PrototypePhase.SongSelect, input);

            Assert.Equal(PrototypeInputSource.MajdataReflection, frame.Source);
            Assert.Equal(
                new[] { PrototypeAction.SongNext, PrototypeAction.SongPrevious, PrototypeAction.Ok, PrototypeAction.Back },
                frame.Actions.ToArray());
        }

        [Fact]
        public void DifficultySelectMapsA3A6ToDifficultyActions()
        {
            RawPrototypeInput input = new RawPrototypeInput(PrototypeInputSource.MajdataReflection, true, false, false, true);

            PrototypeInputFrame frame = PrototypeInputMapper.Map(PrototypePhase.DifficultySelect, input);

            Assert.Equal(new[] { PrototypeAction.DifficultyUp, PrototypeAction.DifficultyDown }, frame.Actions.ToArray());
        }

        [Fact]
        public void ConfirmedOnlyMapsBack()
        {
            RawPrototypeInput input = new RawPrototypeInput(PrototypeInputSource.KeyboardFallback, true, true, true, true);

            PrototypeInputFrame frame = PrototypeInputMapper.Map(PrototypePhase.Confirmed, input);

            Assert.Equal(PrototypeInputSource.KeyboardFallback, frame.Source);
            Assert.Equal(new[] { PrototypeAction.Back }, frame.Actions.ToArray());
        }

        [Fact]
        public void NullInputMapsToNoActions()
        {
            PrototypeInputFrame frame = PrototypeInputMapper.Map(PrototypePhase.SongSelect, null);

            Assert.Equal(PrototypeInputSource.None, frame.Source);
            Assert.Empty(frame.Actions);
        }
    }
}
