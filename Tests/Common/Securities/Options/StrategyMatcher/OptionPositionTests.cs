using System;
using NUnit.Framework;
using QuantConnect.Securities.Option.StrategyMatcher;

namespace QuantConnect.Tests.Common.Securities.Options.StrategyMatcher
{
    [TestFixture]
    public class OptionPositionTests
    {
        [Test]
        public void AdditionOperator_AddsQuantity_WhenSymbolsMatch()
        {
            var left = new OptionPosition(Symbols.SPY, 42);
            var right = new OptionPosition(Symbols.SPY, 1);
            var sum = left + right;
            Assert.AreEqual(Symbols.SPY, sum.Symbol);
            Assert.AreEqual(43, sum.Quantity);
        }

        [Test]
        public void AdditionOperator_ThrowsInvalidOperationException_WhenSymbolsDoNotMatch()
        {
            OptionPosition sum;
            var left = new OptionPosition(Symbols.SPY, 42);
            var right = new OptionPosition(Symbols.SPY_P_192_Feb19_2016, 1);
            Assert.Throws<InvalidOperationException>(
                () => sum = left + right
            );
        }

        [Test]
        public void AdditionOperator_DoesNotThrow_WhenOneSideEqualsDefault()
        {
            var value = new OptionPosition(Symbols.SPY, 42);
            var defaultValue = default(OptionPosition);
            var valueFirst = value + defaultValue;
            var defaultFirst = defaultValue + value;

            Assert.AreEqual(value, valueFirst);
            Assert.AreEqual(value, defaultFirst);
        }
        [Test]
        public void SubtractionOperator_SubtractsQuantity_WhenSymbolsMatch()
        {
            var left = new OptionPosition(Symbols.SPY, 42);
            var right = new OptionPosition(Symbols.SPY, 1);
            var sum = left - right;
            Assert.AreEqual(Symbols.SPY, sum.Symbol);
            Assert.AreEqual(41, sum.Quantity);
        }

        [Test]
        public void SubtractionOperator_ThrowsInvalidOperationException_WhenSymbolsDoNotMatch()
        {
            OptionPosition difference;
            var left = new OptionPosition(Symbols.SPY, 42);
            var right = new OptionPosition(Symbols.SPY_P_192_Feb19_2016, 1);
            Assert.Throws<InvalidOperationException>(
                () => difference = left - right
            );
        }

        [Test]
        public void SubtractionOperator_DoesNotThrow_WhenOneSideEqualsDefault()
        {
            var value = new OptionPosition(Symbols.SPY, 42);
            var defaultValue = default(OptionPosition);
            var valueFirst = value - defaultValue;
            var defaultFirst = defaultValue - value;

            Assert.AreEqual(value, valueFirst);
            Assert.AreEqual(value.Negate(), defaultFirst);
        }

        [Test]
        public void Negate_ReturnsOptionPosition_WithSameSymbolAndNegativeQuantity()
        {
            var position = new OptionPosition(Symbols.SPY, 42);
            var negated = position.Negate();
            Assert.AreEqual(position.Symbol, negated.Symbol);
            Assert.AreEqual(-position.Quantity, negated.Quantity);
        }

        [Test]
        public void MultiplyOperator_ScalesQuantity()
        {
            const int factor = 2;
            var position = new OptionPosition(Symbols.SPY, 42);
            var positionFirst = position * factor;
            Assert.AreEqual(position.Symbol, positionFirst.Symbol);
            Assert.AreEqual(factor * 42, positionFirst.Quantity);

            var factorFirst = factor * position;
            Assert.AreEqual(positionFirst, factorFirst);
        }

        [Test]
        public void Equality_IsDefinedUsing_SymbolAndQuantity()
        {
            var left = new OptionPosition(Symbols.SPY, 42);
            var right = new OptionPosition(Symbols.SPY, 42);
            Assert.AreEqual(left, right);
            Assert.IsTrue(left == right);

            right = right.Negate();
            Assert.AreNotEqual(left, right);
            Assert.IsTrue(left != right);
        }

        [Test]
        public void None_CreatesOptionPosition_WithZeroQuantity()
        {
            var none = OptionPosition.None(Symbols.SPY);
            Assert.AreEqual(0, none.Quantity);
            Assert.AreEqual(Symbols.SPY, none.Symbol);
        }
    }
}
