using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using QuantConnect.Securities.Option.StrategyMatcher;
using static QuantConnect.Securities.Option.StrategyMatcher.OptionStrategyDefinition;
using static QuantConnect.Securities.Option.StrategyMatcher.OptionPositionCollection;
using static QuantConnect.Tests.Common.Securities.Options.StrategyMatcher.Option;

namespace QuantConnect.Tests.Common.Securities.Options.StrategyMatcher
{
    [TestFixture]
    public class OptionStrategyMatcherTests
    {

    }

    [TestFixture]
    public class OptionStrategyDefinitionTests
    {
        [Test]
        public void BearCallSpread()
        {
            // 0: -1 Call
            // 1: +1 Call; strike>=leg[0].strike; expr=leg[0].expr
            //var positions = OptionPositionCollection.Create(Symbols.SPY, ContractMultiplier, Enumerable.Empty<SecurityHolding>())
            //    .Add(new OptionPosition(Symbols.SPY, 1000))
            //    .Add(new OptionPosition(Put[95m], 1))
            //    .Add(new OptionPosition(Put[95m, 1], 1))
            //    .Add(new OptionPosition(Call[95m], 1))
            //    .Add(new OptionPosition(Call[95m, 1], 1))
            //    .Add(new OptionPosition(Call[100m], 1))
            //    .Add(new OptionPosition(Call[100m, 1], 1));

            var positions = new List<OptionPosition>
            {
                new OptionPosition(Option.Call[100m], 5),
                new OptionPosition(Option.Call[95m], 3)
            };

            var expectedQuantity = positions.Min(p => p.Quantity);

            var definition = Create("Bear Call Spread",
                Call(1),
                Call(1, (legs, p) => p.Strike >= legs[0].Strike,
                            (legs, p) => p.Expiration == legs[0].Expiration
                )
            );

            OptionStrategy strategy;
            Assert.IsTrue(definition.TryMatch(positions, out strategy));
            Assert.AreEqual(definition.Name, strategy.Name);
            Assert.AreEqual(0, strategy.UnderlyingLegs.Count);
            Assert.AreEqual(2, strategy.OptionLegs.Count);

            Assert.AreEqual(100m, strategy.OptionLegs[0].Strike);
            Assert.AreEqual(0m, strategy.OptionLegs[0].OrderPrice);
            Assert.AreEqual(OptionRight.Call, strategy.OptionLegs[0].Right);
            Assert.AreEqual(expectedQuantity, strategy.OptionLegs[0].Quantity);
            Assert.AreEqual(OrderType.Market, strategy.OptionLegs[0].OrderType);
            Assert.AreEqual(positions[0].Expiration, strategy.OptionLegs[0].Expiration);

            Assert.AreEqual(95m, strategy.OptionLegs[1].Strike);
            Assert.AreEqual(0m, strategy.OptionLegs[1].OrderPrice);
            Assert.AreEqual(OptionRight.Call, strategy.OptionLegs[1].Right);
            Assert.AreEqual(expectedQuantity, strategy.OptionLegs[1].Quantity);
            Assert.AreEqual(OrderType.Market, strategy.OptionLegs[1].OrderType);
            Assert.AreEqual(positions[1].Expiration, strategy.OptionLegs[1].Expiration);

            var matches = definition.Match(Empty.AddRange(positions)).ToList();
            Assert.AreEqual(1, matches.Count);

            // matching requires the positions to be flipped in the legs list
            Assert.AreEqual(positions[0], matches[0].Legs[1].Position);
            Assert.AreEqual(positions[1], matches[0].Legs[0].Position);
        }

        public class TestCase
        {

        }
    }
}
