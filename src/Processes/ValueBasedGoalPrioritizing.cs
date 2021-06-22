/// Name: ValueBasedGoalPrioritizing.cs
/// Description:
/// Authors: Multiple.
/// Copyright: Garry Sotnik

using System;
using System.Collections.Generic;
using System.Linq;

using SOSIEL.Entities;
using SOSIEL.Enums;
using SOSIEL.Exceptions;
using SOSIEL.Processes;

using SOSIEL.VBGP.Configuration;

namespace SOSIEL.VBGP.Processes
{
    /// <summary>
    /// Value-based goal prioritizing process implementation.
    /// </summary>
    public class ValueBasedGoalPrioritizing : IGoalPrioritizing
    {
        /// <summary>
        /// Configuration object.
        /// </summary>
        private readonly ValueBasedGoalPrioritizingConfiguration _config;

        /// <summary>
        /// Initializes new instance of the ValueBasedGoalPrioritizing class.
        /// </summary>
        /// <param name="config">Process configuration.</param>
        public ValueBasedGoalPrioritizing(ValueBasedGoalPrioritizingConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _config = config;
        }

        /// <summary>
        /// Prioritizes agent goals.
        /// </summary>
        /// <param name="agent">The agent.</param>
        /// <param name="goals">The goals.</param>
        public void Prioritize(IAgent agent, IReadOnlyDictionary<Goal, GoalState> goals)
        {
            if (goals.Count < 1) return;

            foreach (var kvp in goals)
                kvp.Value.AdjustedImportance = kvp.Value.Importance;

            if (!agent.Archetype.UseImportanceAdjusting) return;

            var noConfidenceGoals = goals.Where(kvp => kvp.Value.Importance > 0 && !kvp.Value.Confidence);
            if (noConfidenceGoals.FirstOrDefault().Key != null)
            {
                foreach (var kvp in noConfidenceGoals)
                {
                    var adjustment = CalculateRelativeImportanceLevelAdjustment(kvp.Key, kvp.Value);
                    kvp.Value.AdjustedImportance = kvp.Value.Importance * adjustment;
                }

                var totalAdjustedImportance = goals.Sum(kvp => kvp.Value.AdjustedImportance);
                foreach (var kvp in goals)
                    kvp.Value.AdjustedImportance /= totalAdjustedImportance;
            }
        }


        /// <summary>
        /// Calculates normalized value for goal prioritizing.
        /// </summary>
        /// <param name="goalIndex"></param>
        /// <param name="goal"></param>
        /// <param name="goalState"></param>
        /// <returns>Reative importance level adjustement value.</returns>
        private double CalculateRelativeImportanceLevelAdjustment(Goal goal, GoalState goalState)
        {
            var value = goalState.Value;
            var focalValue = goalState.FocalValue;
            var priorValue = goalState.PriorValue;
            var gainLossMapping = _config.GainAndLossToValue[goal.Name];

            switch (goal.Type)
            {
                case GoalType.Maximize:
                {
                    if (priorValue == 0.0) return 1.0;
                    var loss = Math.Min(0.0, value / priorValue - 1.0);
                    return 1.0 - FindNearestValue(gainLossMapping.LossToValue, loss);
                }

                case GoalType.EqualToOrAboveFocalValue:
                {
                    if (focalValue == 0.0) return 1.0;
                    var loss = Math.Min(0.0, value / focalValue - 1.0);
                    return 1.0 - FindNearestValue(gainLossMapping.LossToValue, loss);
                }

                case GoalType.MaintainAtValue:
                {
                    if (focalValue == 0.0) return 1.0;
                    var k = value / focalValue;
                    return (k >= 1.0)
                        ? 1.0 + FindNearestValue(gainLossMapping.GainToValue, k - 1.0)
                        : 1.0 - FindNearestValue(gainLossMapping.LossToValue, k - 1.0);
                }

                case GoalType.Minimize:
                {
                    if (priorValue == 0.0) return 1.0;
                    var loss = Math.Max(0.0, value / priorValue - 1.0);
                    return 1.0 - FindNearestValue(gainLossMapping.LossToValue, loss);
                }

                default:
                {
                    throw new SosielAlgorithmException(
                        "Cannot calculate relative difference between goal value and focal" +
                        $" goal value for goal type {goal.Type}");
                }
            }
        }

        /// <summary>
        /// Returns mapped value that corresponds to an exact or nearest argument match.
        /// Assumes that "mapping" has at least 1 element.
        /// </summary>
        /// <param name="mapping">A mapping to be searched.</param>
        /// <param name="arg">An argument to search for.</param>
        /// <returns>value that corresponds to an exact or nearest argument match.</returns>
        private static double FindNearestValue(GainOrLossToValueMappingElement[] mapping, double arg)
        {
            var resultIndex = FindNearestValueIndex(mapping, arg);
            return mapping[resultIndex].Value;
        }

        /// <summary>
        /// A maximum difference between two small floating point values to consider them equal.
        /// Obtained in the experimental way by comparing non-constant, computed small values
        /// which should have 3 digits after the point.
        /// </summary>
        private static readonly double kMaxDelta = 6.94e-18;

        /// <summary>
        /// Returns index of element that corresponds to an exact or nearest argument match.
        /// Assumes that "mapping" has at least 1 element.
        /// </summary>
        /// <param name="mapping">A mapping to be searched.</param>
        /// <param name="arg">An argument to search for.</param>
        /// <returns>value that corresponds to an exact or nearest argument match.</returns>
        private static int FindNearestValueIndex(GainOrLossToValueMappingElement[] mapping, double arg)
        {
            if (arg <= mapping[0].Argument) return 0;
            if (arg >= mapping[mapping.Length - 1].Argument) return mapping.Length - 1;

            var left = 0;
            var right = mapping.Length - 1;

            while (true)
            {
                if (Math.Abs(arg - mapping[left].Argument) <= kMaxDelta)
                    return left;

                if (left >= right) break;

                var mid = left + (right - left) / 2;
                var dv = arg - mapping[mid].Argument;
                if (dv < 0 && Math.Abs(dv) > kMaxDelta)
                    right = mid;
                else
                    left = mid + 1;
            }

            var dv1 = Math.Abs(arg - mapping[left - 1].Argument);
            var dv2 = Math.Abs(arg - mapping[left].Argument);
            var ddv = dv1 - dv2;
            if (Math.Abs(ddv) <= kMaxDelta)
                return (mapping[left - 1].Argument < 0) ? left - 1 : left;
            else
                return (dv1 < dv2) ? left - 1 : left;
        }
    }
}
