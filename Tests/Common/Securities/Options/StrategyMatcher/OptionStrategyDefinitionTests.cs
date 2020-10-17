using NUnit.Framework;
using static QuantConnect.Securities.Option.StrategyMatcher.OptionStrategyDefinition;

namespace QuantConnect.Tests.Common.Securities.Options.StrategyMatcher
{
    [TestFixture]
    public class OptionStrategyDefinitionTests
    {
        [Test]
        public void BearCallSpread()
        {
            // 0: Call
            // 1: Call; strike>=leg[0].strike; expr=leg[0].expr
            var definition = Create("Bear Call Spread",
                Call(1),
                Call(1, (legs, p) => p.Strike >= legs[0].Strike,
                        (legs, p) => p.Expiration == legs[0].Expiration
                )
            );
        }
    }
}
