using System;

namespace RecursiveAI.OneMath.Physics;

/// <summary>Input checks shared by the symmetric eigen decomposition and the inertia functions.</summary>
internal static class GuardsD
{
    // Symmetry tolerance: 64 units of machine epsilon times the largest entry. A tensor that was built by
    // rotating or shifting another one in this precision stays well inside it.
    private static readonly double s_symmetryTolerance = SymmetricEigenD.Pow2(-46);

    internal static void Finite(double value, string paramName, string what)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName, what + " is " + (double.IsNaN(value) ? "NaN" : "infinite") + "; a finite value is expected.");
        }
    }

    internal static void Finite(Vector3D v, string paramName, string what)
    {
        Finite(v.X, paramName, what + ".X");
        Finite(v.Y, paramName, what + ".Y");
        Finite(v.Z, paramName, what + ".Z");
    }

    internal static void Finite(Matrix3x3D m, string paramName, string what)
    {
        Finite(m.M11, paramName, what + ".M11");
        Finite(m.M12, paramName, what + ".M12");
        Finite(m.M13, paramName, what + ".M13");
        Finite(m.M21, paramName, what + ".M21");
        Finite(m.M22, paramName, what + ".M22");
        Finite(m.M23, paramName, what + ".M23");
        Finite(m.M31, paramName, what + ".M31");
        Finite(m.M32, paramName, what + ".M32");
        Finite(m.M33, paramName, what + ".M33");
    }

    /// <summary>A mass: finite and not negative (zero is allowed).</summary>
    internal static void Mass(double mass, string paramName, string what)
    {
        Finite(mass, paramName, what);
        if (mass < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, what + " is negative; a mass of zero or more is expected.");
        }
    }

    /// <summary>A length (radius, half extent): finite and not negative.</summary>
    internal static void Length(double length, string paramName, string what)
    {
        Finite(length, paramName, what);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, what + " is negative; a length of zero or more is expected.");
        }
    }

    /// <summary>All entries finite and every mirrored pair equal to within the tolerance (the upper triangle is what the functions use).</summary>
    internal static void Symmetric(Matrix3x3D m, string paramName, string what)
    {
        Finite(m, paramName, what);
        double max = Math.Abs(m.M11);
        max = Max(max, Math.Abs(m.M12));
        max = Max(max, Math.Abs(m.M13));
        max = Max(max, Math.Abs(m.M21));
        max = Max(max, Math.Abs(m.M22));
        max = Max(max, Math.Abs(m.M23));
        max = Max(max, Math.Abs(m.M31));
        max = Max(max, Math.Abs(m.M32));
        max = Max(max, Math.Abs(m.M33));
        double tol = max * s_symmetryTolerance;
        if (Math.Abs(m.M12 - m.M21) > tol)
        {
            throw NotSymmetric(paramName, what, "M12", "M21");
        }
        if (Math.Abs(m.M13 - m.M31) > tol)
        {
            throw NotSymmetric(paramName, what, "M13", "M31");
        }
        if (Math.Abs(m.M23 - m.M32) > tol)
        {
            throw NotSymmetric(paramName, what, "M23", "M32");
        }
    }

    private static double Max(double a, double b) => a > b ? a : b;

    private static ArgumentException NotSymmetric(string paramName, string what, string a, string b) =>
        new(what + " is not symmetric: " + what + "." + a + " and " + what + "." + b + " differ by more than 64 machine epsilons of the largest entry.", paramName);
}
