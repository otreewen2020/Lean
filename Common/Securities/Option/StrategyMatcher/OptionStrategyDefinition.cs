/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using static QuantConnect.Securities.Option.StrategyMatcher.OptionStrategyDefinition;
namespace QuantConnect.Securities.Option.StrategyMatcher
{
    public class OptionStrategyDefinitionMatch
    {
        public OptionStrategyDefinition Definition { get; }
        public IReadOnlyList<OptionStrategyLegDefinitionMatch> Legs { get; }

        public OptionStrategyDefinitionMatch(OptionStrategyDefinition definition, IReadOnlyList<OptionStrategyLegDefinitionMatch> legs)
        {
            Legs = legs;
            Definition = definition;
        }
    }

    public static class OptionStrategyDefinitions
    {
        public static readonly OptionStrategyDefinition BearCallSpread
            = Create("Bear Call Spread",
                Call(-1),
                Call(+1, (legs, p) => legs[0].Strike >= p.Strike,
                         (legs, p) => legs[0].Expiration == p.Expiration)
            );

        public static readonly OptionStrategyDefinition BearPutSpread
            = Create("Bear Put Spread",
                Put(1),
                Put(-1, (legs, p) => legs[0].Strike <= p.Strike,
                        (legs, p) => legs[0].Expiration == p.Expiration)
            );

        public static readonly OptionStrategyDefinition BullCallSpread
            = Create("Bull Call Spread",
                Call(+1),
                Call(-1, (legs, p) => legs[0].Strike >= p.Strike,
                         (legs, p) => legs[0].Expiration == p.Expiration)
            );

        public static readonly OptionStrategyDefinition BullPutSpread
            = Create("Bull Put Spread",
                Put(-1),
                Put(+1, (legs, p) => legs[0].Strike >= p.Strike,
                        (legs, p) => legs[0].Expiration == p.Expiration)
            );

        public static readonly OptionStrategyDefinition Straddle
            = Create("Straddle",
                Call(+1),
                Put(-1, (legs, p) => legs[0].Strike == p.Strike,
                        (legs, p) => legs[0].Expiration == p.Expiration)
            );

        public static readonly OptionStrategyDefinition Strangle
            = Create("Strangle",
                Call(+1),
                Put(+1, (legs, p) => legs[0].Strike >= p.Strike,
                        (legs, p) => legs[0].Expiration == p.Expiration)
            );

        public static readonly OptionStrategyDefinition CallButterfly
            = Create("Call Butterfly",
                Call(+1),
                Call(-2, (legs, p) => legs[0].Strike <= p.Strike,
                         (legs, p) => legs[0].Expiration == p.Expiration),
                Call(+1, (legs, p) => legs[0].Strike >= p.Strike,
                         (legs, p) => legs[0].Expiration == p.Expiration,
                         (legs, p) => legs[0].Strike - legs[1].Strike == legs[1].Strike - legs[2].Strike)
            );

        public static readonly OptionStrategyDefinition PutButterfly
            = Create("Put Butterfly",
                Put(+1),
                Put(-2, (legs, p) => legs[0].Strike <= p.Strike,
                        (legs, p) => legs[0].Expiration == p.Expiration),
                Put(+1, (legs, p) => legs[0].Strike >= p.Strike,
                        (legs, p) => legs[0].Expiration == p.Expiration,
                        (legs, p) => legs[0].Strike - legs[1].Strike == legs[1].Strike - legs[2].Strike)
            );

        public static readonly OptionStrategyDefinition CallCalendarSpread
            = Create("Call Calendar Spread",
                Call(+1),
                Call(+1, (legs, p) => legs[0].Strike == p.Strike,
                         (legs, p) => legs[0].Expiration >= p.Expiration)
            );

        public static readonly OptionStrategyDefinition PutCalendarSpread
            = Create("Put Calendar Spread",
                Put(+1),
                Put(+1, (legs, p) => legs[0].Strike == p.Strike,
                    (legs, p) => legs[0].Expiration >= p.Expiration)
            );
    }

    public class OptionStrategyDefinition
    {
        public string Name { get; }

        private readonly int _underlyingLots;
        private readonly List<OptionStrategyLegDefinition> _legs;

        public OptionStrategyDefinition(string name, int underlyingLots, IEnumerable<OptionStrategyLegDefinition> legs)
        {
            Name = name;
            _legs = legs.ToList();
            _underlyingLots = underlyingLots;
        }

        public OptionStrategy CreateStrategy(IReadOnlyList<OptionStrategyLegDefinitionMatch> legs)
        {
            var underlying = legs[0].Position.Symbol;
            if (underlying.HasUnderlying)
            {
                underlying = underlying.Underlying;
            }

            var strategy = new OptionStrategy {Name = Name, Underlying = underlying};
            for (int i = 0; i < Math.Min(_legs.Count, legs.Count); i++)
            {
                var leg = _legs[i].CreateLegData(legs[i]);
                leg.Invoke(strategy.UnderlyingLegs.Add, strategy.OptionLegs.Add);
            }

            return strategy;
        }

        /// <summary>
        /// Determines all possible matches for this definition using the provided <paramref name="positions"/>
        /// </summary>
        public IEnumerable<OptionStrategyDefinitionMatch> Match(OptionPositionCollection positions)
        {
            // immediately match required underlying lots

            return Match(
                ImmutableList<OptionStrategyLegDefinitionMatch>.Empty,
                ImmutableList<OptionPosition>.Empty,
                positions,
                int.MaxValue
            );
        }

        private IEnumerable<OptionStrategyDefinitionMatch> Match(
            ImmutableList<OptionStrategyLegDefinitionMatch> legMatches,
            ImmutableList<OptionPosition> legPositions,
            OptionPositionCollection positions,
            int multiplier
            )
        {
            if (legPositions.Count == _legs.Count)
            {
                if (legPositions.Count > 0)
                {
                    yield return new OptionStrategyDefinitionMatch(this, legMatches);
                }
            }
            else if (!positions.IsEmpty)
            {
                foreach (var match in _legs[legPositions.Count].Match(legPositions, positions))
                {
                    // add match to the match we're constructing and deduct matched position from positions collection
                    // we track the min multiplier in line so when we're done, we have the total number of matches for
                    // the matched set of positions in this 'thread' (OptionStrategy.Quantity)
                    foreach (var strategy in Match(
                        legMatches.Add(match),
                        legPositions.Add(match.Position),
                        positions - match.Position,
                        Math.Min(multiplier, match.Multiplier)
                    ))
                    {
                        yield return strategy;
                    }
                }
            } /*
            else
            {
                // if positions.IsEmpty indicates a failed match

                // could include partial matches, would allow an algorithm to determine if adding a
                // new position could help reduce overall margin exposure by completing a strategy
            } */
        }

        /// <summary>
        /// Attempts to exactly match the specified positions to this strategy definition with as much quantity as possible.
        /// </summary>
        public bool TryMatch(IReadOnlyList<OptionPosition> positions, out OptionStrategy strategy)
        {
            if (positions.Count == 0 || _legs.Count != positions.Count)
            {
                strategy = null;
                return false;
            }

            var underlying = positions[0].Symbol;
            if (underlying.SecurityType == SecurityType.Option)
            {
                underlying = underlying.Underlying;
            }

            var quantityMultiplier = int.MaxValue;
            var matches = new List<OptionStrategy.LegData>();
            for (int i = 0; i < _legs.Count; i++)
            {
                var leg = _legs[i];
                var position = positions[i];
                OptionStrategy.LegData match;
                if (!leg.TryMatch(position, out match))
                {
                    strategy = null;
                    return false;
                }

                matches.Add(match);
                var multiple = match.Quantity / leg.Quantity;
                quantityMultiplier = Math.Min(multiple, quantityMultiplier);
            }

            // separate matches into option/underlying legs and resize according to smallest quantity multipler
            var optionLegs = new List<OptionStrategy.OptionLegData>();
            var underlyingLegs = new List<OptionStrategy.UnderlyingLegData>();
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                match.Invoke(underlyingLegs.Add, optionLegs.Add);
                match.Quantity = _legs[i].Quantity * quantityMultiplier;
            }

            strategy = new OptionStrategy
            {
                Name = Name,
                OptionLegs = optionLegs,
                Underlying =  underlying,
                UnderlyingLegs = underlyingLegs
            };

            return true;
        }

        public static OptionStrategyDefinition Create(string name, int underlyingLots, params OptionStrategyLegDefinition[] legs)
        {
            return new OptionStrategyDefinition(name, underlyingLots, legs);
        }

        public static OptionStrategyDefinition Create(string name, params OptionStrategyLegDefinition[] legs)
        {
            return new OptionStrategyDefinition(name, 0, legs);
        }

        public static OptionStrategyDefinition Create(string name, params Func<Builder, Builder>[] predicates)
        {
            return predicates.Aggregate(new Builder(name),
                (builder, predicate) => predicate(builder)
            ).Build();
        }

        public static OptionStrategyLegDefinition Call(int quantity,
            params Expression<Func<IReadOnlyList<OptionPosition>, OptionPosition, bool>>[] predicates
            )
        {
            return OptionStrategyLegDefinition.Create(OptionRight.Call, quantity, predicates);
        }

        public static OptionStrategyLegDefinition Put(int quantity,
            params Expression<Func<IReadOnlyList<OptionPosition>, OptionPosition, bool>>[] predicates
            )
        {
            return OptionStrategyLegDefinition.Create(OptionRight.Put, quantity, predicates);
        }

        public class Builder
        {
            private readonly string _name;

            private int _underlyingLots;
            private List<OptionStrategyLegDefinition> _legs;

            public Builder(string name)
            {
                _name = name;
                _legs = new List<OptionStrategyLegDefinition>();
            }

            public Builder WithUnderlyingLots(int lots)
            {
                if (_underlyingLots != 0)
                {
                    throw new InvalidOperationException("Underlying lots has already been set.");
                }

                _underlyingLots = lots;
                return this;
            }

            public Builder WithCall(int quantity,
                params Expression<Func<IReadOnlyList<OptionPosition>, OptionPosition, bool>>[] predicates
                )
            {
                _legs.Add(OptionStrategyLegDefinition.Create(OptionRight.Call, quantity, predicates));
                return this;
            }

            public Builder WithPut(int quantity,
                params Expression<Func<IReadOnlyList<OptionPosition>, OptionPosition, bool>>[] predicates
                )
            {
                _legs.Add(OptionStrategyLegDefinition.Create(OptionRight.Put, quantity, predicates));
                return this;
            }

            public OptionStrategyDefinition Build()
            {
                return new OptionStrategyDefinition(_name, _underlyingLots, _legs);
            }
        }
    }
}
