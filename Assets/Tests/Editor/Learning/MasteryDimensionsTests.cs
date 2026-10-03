using System.Linq;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class MasteryDimensionsTests
    {
        [Test]
        public void For_Symbol_ReturnsFormSoundAndAssembly()
        {
            Assert.That(MasteryDimensions.For(LearningContentKind.Symbol).ToArray(),
                Is.EqualTo(new[]
                {
                    MasteryDimension.Form,
                    MasteryDimension.Sound,
                    MasteryDimension.Assembly,
                }));
        }

        [Test]
        public void For_Word_ReturnsAllFourDimensions()
        {
            Assert.That(MasteryDimensions.For(LearningContentKind.Word).ToArray(),
                Is.EqualTo(new[]
                {
                    MasteryDimension.Form,
                    MasteryDimension.Sound,
                    MasteryDimension.Assembly,
                    MasteryDimension.Meaning,
                }));
        }

        [Test]
        public void IsApplicable_SymbolMeaningIsFalse()
        {
            Assert.That(MasteryDimensions.IsApplicable(
                LearningContentKind.Symbol, MasteryDimension.Meaning), Is.False);
        }
    }
}
