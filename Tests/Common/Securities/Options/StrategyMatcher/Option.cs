using System;

namespace QuantConnect.Tests.Common.Securities.Options.StrategyMatcher
{
    /// <summary>
    /// Provides array-indexer calling conventions for easily creating option contract symbols.
    /// I suspect I'll update this later to fulfill the original vision of being a full option
    /// chain, where indexing is successively applied, such as Puts[100m] would return a dictionary
    /// keyed by expiration of all puts@100. To pull a specific one, Puts[100m][expiration] or Puts[100m][1]
    /// using the weeks notation used in the indexers in this class.
    /// </summary>
    public static class Option
    {
        public static readonly DateTime ReferenceDate = new DateTime(2020, 10, 16);

        public const decimal ContractMultiplier = 100m;
        public static Factory Contract { get; } = new Factory();
        public static FactoryRight Put { get; } = new FactoryRight(OptionRight.Put);
        public static FactoryRight Call { get; } = new FactoryRight(OptionRight.Call);

        public class Factory
        {
            public Symbol this[Symbol underlying, OptionRight right, decimal strike, DateTime expiration]
                => Symbol.CreateOption(underlying, underlying.ID.Market, OptionStyle.American, right, strike, expiration);

            public Symbol this[OptionRight right, decimal strike, DateTime expiration]
                => Symbol.CreateOption(Symbols.SPY, Market.USA, OptionStyle.American, right, strike, expiration);

            public Symbol this[Symbol underlying, OptionRight right, decimal strike, int weeks = 0]
                => Symbol.CreateOption(underlying, underlying.ID.Market, OptionStyle.American, right, strike, ReferenceDate.AddDays(7 * weeks));

            public Symbol this[OptionRight right, decimal strike, int weeks = 0]
                => Symbol.CreateOption(Symbols.SPY, Market.USA, OptionStyle.American, right, strike, ReferenceDate.AddDays(7 * weeks));
        }

        public class FactoryRight
        {
            private readonly OptionRight right;

            public FactoryRight(OptionRight right)
            {
                this.right = right;
            }

            public Symbol this[Symbol underlying, decimal strike, DateTime expiration]
                => Symbol.CreateOption(underlying, underlying.ID.Market, OptionStyle.American, right, strike, expiration);

            public Symbol this[decimal strike, DateTime expiration]
                => Symbol.CreateOption(Symbols.SPY, Market.USA, OptionStyle.American, right, strike, expiration);

            public Symbol this[Symbol underlying, decimal strike, int weeks = 0]
                => Symbol.CreateOption(underlying, underlying.ID.Market, OptionStyle.American, right, strike, ReferenceDate.AddDays(7 * weeks));

            public Symbol this[decimal strike, int weeks = 0]
                => Symbol.CreateOption(Symbols.SPY, Market.USA, OptionStyle.American, right, strike, ReferenceDate.AddDays(7 * weeks));
        }
    }
}
