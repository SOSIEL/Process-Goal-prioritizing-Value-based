// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2021 SOSIEL Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SOSIEL.VBGP.Configuration
{
    /// <summary>
    /// Gain/Loss to GainValue/LossValue mapping element.
    /// </summary>
    public struct GainOrLossToValueMappingElement
    {
        /// <summary>
        /// Argument, either gain or loss.
        /// </summary>
        public double Argument;

        /// <summary>
        /// Associated value.
        /// </summary>
        public double Value;
    };


    /// <summary>
    /// Gain and Loss to Value mapping for a single goal.
    /// </summary>
    public class GoalGainOrLossToValueMapping
    {
        /// <summary>
        /// Index of the goal to which this mapping applies.
        /// </summary>
        string GoalName { get; }

        /// <summary>
        /// Gain to Value mapping.
        /// </summary>
        public GainOrLossToValueMappingElement[] GainToValue { get; }

        /// <summary>
        /// Loss to Value mapping.
        /// </summary>
        public GainOrLossToValueMappingElement[] LossToValue { get; }

        /// <summary>
        /// Initializes a new instance of the GoalGainOrLossToValueMapping class.
        /// </summary>
        /// <param name="goalName">Name of the goal to which this mapping applies.</param>
        /// <param name="elements">Elements of the mapping.</param>
        public GoalGainOrLossToValueMapping(string goalName, GainOrLossToValueMappingElement[] elements)
        {
            if (goalName == null)
                throw new ArgumentNullException("Missing goal name", nameof(goalName));

            if (elements == null)
                throw new ArgumentNullException(nameof(elements), $"Missing mapping for the goal '{goalName}'");

            if (elements.Length == 0)
                throw new ArgumentException( $"Empty mapping for the goal '{goalName}'", nameof(elements));

            var uniqueArgs = new Dictionary<double, int>();
            for (int j = 0; j < elements.Length; ++j)
            {
                var e = elements[j];

                if (e.Argument < -100.0 || e.Argument > 100.0)
                {
                    throw new ArgumentException(
                        $"Invalid mapping argument #{j} '{e.Argument}' for the goal '{goalName}'",
                        nameof(elements));
                }

                if (uniqueArgs.ContainsKey(e.Argument))
                {
                    throw new ArgumentException(
                        $"Duplicate mapping argument #{j} '{e.Argument}', introduced earlier at position" +
                        $" #{uniqueArgs[e.Argument]} for the goal '{goalName}'",
                        nameof(elements));
                }

                uniqueArgs.Add(e.Argument, j);
            }

            var sortedElements = elements.OrderBy(e => e.Argument).ToArray();
            int firstGainIndex = 0;
            while (firstGainIndex < sortedElements.Length)
            {
                if (sortedElements[firstGainIndex].Argument >= 0.0) break;
                ++firstGainIndex;
            }

            if (firstGainIndex == 0)
                throw new ArgumentException($"Missing losses in the mapping for the goal '{goalName}'");

            if (firstGainIndex == sortedElements.Length)
                throw new ArgumentException($"Missing gains in the mapping for the goal '{goalName}'");

            GoalName = goalName;
            LossToValue = AdjustNumericRange(sortedElements.Take(firstGainIndex).ToArray());
            GainToValue = AdjustNumericRange(sortedElements.Skip(firstGainIndex).ToArray());
        }

        /// <summary>
        /// Adjusts in-place mapping argument and values into the numeric range [0.0, 1.0].
        /// </summary>
        /// <param name="mapping">A mapping to adjust.</param>
        /// <returns>The same mapping.</returns>
        private static GainOrLossToValueMappingElement[] AdjustNumericRange(GainOrLossToValueMappingElement[] mapping)
        {
            for (int i = 0; i < mapping.Length; ++i)
            {
                mapping[i].Argument /= 100.0;
                mapping[i].Value /= 100.0;
            }
            return mapping;
        }
    }

    /// <summary>
    /// Configuration for the value-based goal prioritizing algorithm.
    /// </summary>
    public class VBGPConfiguration
    {
        /// <summary>
        /// Gain or Loss to Value mappings for the each goal.
        /// </summary>
        public IReadOnlyDictionary<string, GoalGainOrLossToValueMapping> GainAndLossToValue { get; }

        /// <summary>
        /// Number of goals.
        /// </summary>
        public int GoalCount => GainAndLossToValue.Count;

        /// <summary>
        /// Initializes a new instance of the ValueBasedGoalPrioritizingConfiguration class.
        /// </summary>
        /// <param name="gainOrLossToValue">Gain or Loss to corresponding Value mapping elements for each goal.</param>
        public VBGPConfiguration(IReadOnlyDictionary<string, GainOrLossToValueMappingElement[]> gainOrLossToValue)
        {
            // Check that we've got some mappings
            if (gainOrLossToValue == null)
                throw new ArgumentNullException(nameof(gainOrLossToValue));

            // Check that we've got at least one goal
            if (gainOrLossToValue.Count == 0)
                throw new ArgumentException("Missing gain and loss to value mapping", nameof(gainOrLossToValue));

            // Build mappings per goal
            var mappings = new Dictionary<string, GoalGainOrLossToValueMapping>();
            foreach (var kvp in gainOrLossToValue)
                mappings.Add(kvp.Key, new GoalGainOrLossToValueMapping(kvp.Key, kvp.Value));

            GainAndLossToValue = mappings;
        }
    }
}
