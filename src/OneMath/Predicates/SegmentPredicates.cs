using System;

namespace RecursiveAI.OneMath.Predicates;

/// <summary>How two closed segments relate; see <see cref="SegmentPredicates.Classify"/>.</summary>
public enum SegmentRelation
{
    /// <summary>The segments share no point.</summary>
    Disjoint = 0,

    /// <summary>The segments cross at a single point interior to both.</summary>
    Proper = 1,

    /// <summary>
    /// The segments share exactly one point and at least one of the segments has it as an endpoint
    /// (a T-junction, a shared endpoint, or end-to-end collinear contact).
    /// </summary>
    Touching = 2,

    /// <summary>The segments are collinear and share a sub-segment of positive length.</summary>
    CollinearOverlap = 3,
}

/// <summary>Exact segment relations built on <see cref="ExactPredicates.Orient2D"/>.</summary>
public static class SegmentPredicates
{
    /// <summary>
    /// Classifies closed segments ab and cd exactly. Degenerate segments (a == b or c == d) are treated as
    /// points: a point on a segment, or equal to another point, is <see cref="SegmentRelation.Touching"/>; otherwise
    /// <see cref="SegmentRelation.Disjoint"/>. The relation is symmetric and invariant under swapping the
    /// endpoints of either segment.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN, infinite, or outside the range
    /// documented on <see cref="ExactPredicates"/> (checked up front, so the result never depends on which branch ran).</exception>
    public static SegmentRelation Classify(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        ExactPredicates.Check(ax); ExactPredicates.Check(ay); ExactPredicates.Check(bx); ExactPredicates.Check(by);
        ExactPredicates.Check(cx); ExactPredicates.Check(cy); ExactPredicates.Check(dx); ExactPredicates.Check(dy);

        bool abPoint = ax == bx && ay == by;
        bool cdPoint = cx == dx && cy == dy;
        if (abPoint && cdPoint)
        {
            return ax == cx && ay == cy ? SegmentRelation.Touching : SegmentRelation.Disjoint;
        }
        if (abPoint)
        {
            return PointOnSegment(ax, ay, cx, cy, dx, dy) ? SegmentRelation.Touching : SegmentRelation.Disjoint;
        }
        if (cdPoint)
        {
            return PointOnSegment(cx, cy, ax, ay, bx, by) ? SegmentRelation.Touching : SegmentRelation.Disjoint;
        }

        int o1 = ExactPredicates.Orient2D(ax, ay, bx, by, cx, cy);
        int o2 = ExactPredicates.Orient2D(ax, ay, bx, by, dx, dy);
        if (o1 * o2 > 0)
        {
            return SegmentRelation.Disjoint; // c and d on the same strict side of line ab
        }
        int o3 = ExactPredicates.Orient2D(cx, cy, dx, dy, ax, ay);
        int o4 = ExactPredicates.Orient2D(cx, cy, dx, dy, bx, by);
        if (o3 * o4 > 0)
        {
            return SegmentRelation.Disjoint;
        }

        if (o1 == 0 && o2 == 0)
        {
            // Both segments lie on one line (o3 and o4 are then zero too). Along a line, lexicographic
            // (x, then y) order is the order along the line, so compare the intervals' ends that way.
            Order(ref ax, ref ay, ref bx, ref by);
            Order(ref cx, ref cy, ref dx, ref dy);
            // Larger of the two lower ends, smaller of the two upper ends.
            int lo = Cmp(ax, ay, cx, cy) >= 0 ? 1 : 2;
            int hi = Cmp(bx, by, dx, dy) <= 0 ? 1 : 2;
            double lox = lo == 1 ? ax : cx, loy = lo == 1 ? ay : cy;
            double hix = hi == 1 ? bx : dx, hiy = hi == 1 ? by : dy;
            int c = Cmp(lox, loy, hix, hiy);
            return c < 0 ? SegmentRelation.CollinearOverlap : c == 0 ? SegmentRelation.Touching : SegmentRelation.Disjoint;
        }

        // Not collinear: the segments meet only if each straddles or touches the other's line.
        if (o1 * o2 < 0 && o3 * o4 < 0)
        {
            return SegmentRelation.Proper;
        }
        return SegmentRelation.Touching;
    }

    private static bool PointOnSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        if (ExactPredicates.Orient2D(ax, ay, bx, by, px, py) != 0)
        {
            return false;
        }
        // Collinear: within the bounding box of the segment (exact comparisons of the inputs).
        return px >= Math.Min(ax, bx) && px <= Math.Max(ax, bx) && py >= Math.Min(ay, by) && py <= Math.Max(ay, by);
    }

    private static int Cmp(double x0, double y0, double x1, double y1) =>
        x0 < x1 ? -1 : x0 > x1 ? 1 : y0 < y1 ? -1 : y0 > y1 ? 1 : 0;

    private static void Order(ref double x0, ref double y0, ref double x1, ref double y1)
    {
        if (Cmp(x0, y0, x1, y1) > 0)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }
    }
}
