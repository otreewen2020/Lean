using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Defines a complete result from running the matcher on a collection of positions.
    /// The matching process will return one these matches for every potential combination
    /// of strategies conforming to the search settings and the positions provided.
    /// </summary>
    public class OptionStrategyMatch
    {
        public List<OptionStrategy> Strategies { get; }

        public OptionStrategyMatch(List<OptionStrategy> strategies)
        {
            Strategies = strategies;
        }
    }

    /// <summary>
    /// Defines options that influence how the matcher operates.
    /// </summary>
    public class OptionStrategyMatcherOptions
    {
        /// <summary>
        /// The maximum amount of time spent trying to find an optimal solution.
        /// </summary>
        public TimeSpan MaximumDuration { get; }

        /// <summary>
        /// The maximum number of matches to evaluate for the entire portfolio.
        /// </summary>
        public int MaximumSolutionCount { get; }

        /// <summary>
        /// Indexed by leg index, defines the max matches to evaluate per leg.
        /// For example, MaximumCountPerLeg[1] is the max matches to evaluate
        /// for the second leg (index=1).
        /// </summary>
        public IReadOnlyList<int> MaximumCountPerLeg { get; }

        /// <summary>
        /// The definitions to be used for matching.
        /// </summary>
        public IReadOnlyList<OptionStrategyDefinition> Definitions { get; }
    }

    /// <summary>
    /// Evaluates the provided match to assign an objective score. Higher scores are better.
    /// </summary>
    public interface IOptionStrategyMatchObjectiveFunction
    {
        decimal ComputeScore(OptionPositionCollection input, OptionStrategyMatch match);
    }

    /// <summary>
    /// Enumerates an <see cref="OptionPositionCollection"/>. The intent is to evaluate positions that
    /// may be more important sooner. Positions appearing earlier in the enumeration are evaluated before
    /// positions showing later. This effectively prioritizes individual positions. This should not be
    /// used filter filtering, but it could also be used to split a position, for example a position with
    /// 10 could be changed to two 5s and they don't need to be enumerated back to-back either. In this
    /// way you could prioritize the first 5 and then delay matching of the final 5.
    /// </summary>
    public interface IOptionPositionCollectionEnumerator
    {
        IEnumerable<OptionPosition> Enumerate(OptionPositionCollection positions);
    }

    public class DefaultOptionPositionCollectionEnumerator : IOptionPositionCollectionEnumerator
    {
        public IEnumerable<OptionPosition> Enumerate(OptionPositionCollection positions)
        {
            return positions;
        }
    }

    public class AbsoluteRiskOptionPositionCollectionEnumerator : IOptionPositionCollectionEnumerator
    {
        private readonly Func<Symbol, decimal> _marketPriceProvider;

        public AbsoluteRiskOptionPositionCollectionEnumerator(Func<Symbol, decimal> marketPriceProvider)
        {
            _marketPriceProvider = marketPriceProvider;
        }

        public IEnumerable<OptionPosition> Enumerate(OptionPositionCollection positions)
        {
            if (positions.IsEmpty)
            {
                yield break;
            }

            var marketPrice = _marketPriceProvider(positions.Underlying);

            var longPositions = new List<OptionPosition>();
            var shortPuts = new SortedDictionary<decimal, OptionPosition>();
            var shortCalls = new SortedDictionary<decimal, OptionPosition>();
            foreach (var position in positions)
            {
                if (!position.Symbol.HasUnderlying)
                {
                    yield return position;
                }

                if (position.Quantity > 0)
                {
                    longPositions.Add(position);
                }
                else
                {
                    switch (position.Right)
                    {
                        case OptionRight.Put:
                            shortPuts.Add(position.Strike, position);
                            break;

                        case OptionRight.Call:
                            shortCalls.Add(position.Strike, position);
                            break;

                        default:
                            throw new ApplicationException(
                                "The skies are falling, the oceans rising - you're having a bad time"
                            );
                    }
                }
            }
        }
    }

    public class OptionStrategyMatcher
    {
        public OptionStrategyMatcherOptions Options { get; }

        public OptionStrategyMatcher(OptionStrategyMatcherOptions options)
        {
            Options = options;
        }

        //public OptionStrategyMatch FindBestMatch(OptionPositionCollection positions)
        //{

        //}

        public IEnumerable<OptionStrategyMatch> Match(OptionPositionCollection positions)
        {
            foreach (var definition in Options.Definitions)
            {

            }

            yield break;
        }

        public IReadOnlyList<OptionStrategyMatch> MatchOnce(OptionPositionCollection positions)
        {
            var matches = new List<OptionStrategyMatch>();
            foreach (var definition in Options.Definitions)
            {
                while (true)
                {
                    using (var enumerator = definition.Match(positions).GetEnumerator())
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }

                        var match = enumerator.Current;
                        positions = positions.Accept(match);

                    }
                }
            }

            return null;
        }
    }
}
