using System;
using System.Collections.Generic;
using System.Linq;

namespace BehindTheCurtain.Core
{
    public enum ResponseCurveKind { Linear, Quadratic, Logistic, Step }

    public sealed class ResponseCurve
    {
        /// <summary>
        /// Linear: a*x+b. Quadratic: a*x*x+b*x+c. Logistic: 1/(1+exp(-a*(x-b))).
        /// Step: x >= a ? b : c. Parameters are explicit design choices. Output is clamped to [0,1].
        /// </summary>
        public ResponseCurve(ResponseCurveKind kind, double a, double b, double c = 0)
        { Kind = kind; A = a; B = b; C = c; }
        public ResponseCurveKind Kind { get; }
        public double A { get; }
        public double B { get; }
        public double C { get; }
        internal bool IsValid => Kind >= ResponseCurveKind.Linear && Kind <= ResponseCurveKind.Step
            && AiNumber.Finite(A) && AiNumber.Finite(B) && AiNumber.Finite(C);

        public AiResult<double> Evaluate(double input)
        {
            if (!IsValid) return AiResult<double>.Fail(AiFailure.InvalidCurve);
            if (!AiNumber.Finite(input) || input < 0 || input > 1)
                return AiResult<double>.Fail(AiFailure.InvalidInput);
            double value;
            switch (Kind)
            {
                case ResponseCurveKind.Linear: value = A * input + B; break;
                case ResponseCurveKind.Quadratic: value = A * input * input + B * input + C; break;
                case ResponseCurveKind.Logistic: value = 1 / (1 + Math.Exp(-A * (input - B))); break;
                default: value = input >= A ? B : C; break;
            }
            if (double.IsNaN(value)) return AiResult<double>.Fail(AiFailure.InvalidCurve);
            return AiResult<double>.Ok(AiNumber.Clamp(value));
        }
    }

    public sealed class UtilityContext
    {
        internal UtilityContext(World world, AgentState agent, Blackboard blackboard, long tick)
        { World = world; Agent = agent; Blackboard = blackboard; Tick = tick; }
        public World World { get; }
        public AgentState Agent { get; }
        public Blackboard Blackboard { get; }
        public long Tick { get; }
    }

    public sealed class UtilityConsideration
    {
        /// <summary>Input must be pure, deterministic and return a finite value in [0,1].</summary>
        public UtilityConsideration(string id, Func<UtilityContext, double> input, ResponseCurve curve)
        { Id = id; Input = input; Curve = curve; }
        public string Id { get; }
        public Func<UtilityContext, double> Input { get; }
        public ResponseCurve Curve { get; }
    }

    public sealed class UtilityAction
    {
        public UtilityAction(string id, IEnumerable<UtilityConsideration> considerations, double baseWeight)
        {
            Id = id; BaseWeight = baseWeight;
            Considerations = considerations == null ? null! : Array.AsReadOnly(considerations.ToArray());
        }
        public string Id { get; }
        public IReadOnlyList<UtilityConsideration> Considerations { get; }
        public double BaseWeight { get; }
    }

    public sealed class UtilitySettings
    {
        // Provisional design values, chosen arbitrarily until playtesting.
        public const long DefaultReevaluationIntervalTicks = 6;
        public const double DefaultPersistenceBonus = 0.1;
        public UtilitySettings(long reevaluationIntervalTicks = DefaultReevaluationIntervalTicks,
            double persistenceBonus = DefaultPersistenceBonus)
        { ReevaluationIntervalTicks = reevaluationIntervalTicks; PersistenceBonus = persistenceBonus; }
        public long ReevaluationIntervalTicks { get; }
        public double PersistenceBonus { get; }
        internal bool IsValid => ReevaluationIntervalTicks > 0 && AiNumber.Weight(PersistenceBonus);
    }
}
