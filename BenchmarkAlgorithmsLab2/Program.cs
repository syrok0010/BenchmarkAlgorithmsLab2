#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<Benchy>();

// var n = int.Parse(Console.ReadLine()!);
// var rectangles = new List<Rectangle>(n);
// for (int i = 0; i < n; i++)
// {
//     var nums = Console.ReadLine().Split().Select(int.Parse).ToList();
//     rectangles.Add(new Rectangle(new Point(nums[0], nums[1]), new Point(nums[2], nums[3])));
// }
// var m = int.Parse(Console.ReadLine()!);
// var points = new List<Point>();
// for (var i = 0; i < m; i++)
// {
//     var nums = Console.ReadLine().Split(); 
//     points.Add(new Point(int.Parse(nums[0]), int.Parse(nums[1])));
// }
// var solution = SegmentTreeSolution.Precompute(rectangles);
// var answers = new List<int>(m);
// answers.AddRange(points.Select(point => solution.Query(point)));
// Console.WriteLine(string.Join(" ", answers));

[PlainExporter]
[HtmlExporter]
[RPlotExporter]
[MemoryDiagnoser(false)]
public class Benchy
{
    public int PointsCount { get; set; } = 1000;

    [Params(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] 
    public int I { get; set; }

    public int RectanglesCount => (int)Math.Pow(2, I);

    public List<Point> Points = null!;

    public List<Rectangle> Rectangles;

    [GlobalSetup]
    public void Prepare()
    {
        Points =
            Enumerable.Range(0, PointsCount).Select(i =>
            {
                var x = BigInteger.ModPow(9613 * new BigInteger(i), 31, PointsCount * 20);
                var y = BigInteger.ModPow(5227 * new BigInteger(i), 31, PointsCount * 20);
                return new Point((int)x, (int)y);
            }).ToList();
        Rectangles = Enumerable.Range(0, RectanglesCount)
            .Select(i =>
                new Rectangle(
                    new Point(10 * i, 10 * i),
                    new Point(2 * RectanglesCount - i, 2 * RectanglesCount - i)
                )
            )
            .ToList();
        _naiveSolution = NaiveSolution.Precompute(Rectangles);
        _mapSolution = MapSolution.Precompute(Rectangles);
        _treeSolution = SegmentTreeSolution.Precompute(Rectangles);
    }

    private ISolution _naiveSolution = null!;

    [Benchmark]
    public void Query_Naive()
    {
        foreach (var point in Points)
            _naiveSolution.Query(point);
    }

    private ISolution _mapSolution = null!;

    [Benchmark]
    public void Query_Map()
    {
        foreach (var point in Points)
            _mapSolution.Query(point);
    }

    [Benchmark]
    public void Precompute_Map()
    {
        MapSolution.Precompute(Rectangles);
    }

    private ISolution _treeSolution = null!;

    [Benchmark]
    public void Query_Tree()
    {
        foreach (var point in Points)
            _treeSolution.Query(point);
    }

    [Benchmark]
    public void Precompute_Tree()
    {
        SegmentTreeSolution.Precompute(Rectangles);
    }
}

public class NaiveSolution : ISolution
{
    private NaiveSolution(IEnumerable<Rectangle> rectangles) => _rectangles = rectangles;
    private readonly IEnumerable<Rectangle> _rectangles;

    public static ISolution Precompute(IReadOnlyCollection<Rectangle> rectangles)
        => new NaiveSolution(rectangles);

    public int Query(Point point) => _rectangles.Count(r => r.Contains(point));
}

public class MapSolution : ISolution
{
    private readonly List<int> _xs, _ys;
    private readonly int[,] _map;
    private readonly Dictionary<int, int> _xToIndex = new(), _yToIndex = new();

    private MapSolution(List<int> xs, List<int> ys, int[,] map)
        => (_xs, _ys, _map) = (xs, ys, map);

    public static ISolution Precompute(IReadOnlyCollection<Rectangle> rectangles)
    {
        var xs = rectangles.SelectMany(r => new[] { r.BottomLeft.X, r.TopRight.X }).Distinct().OrderBy(x => x).ToList();
        var ys = rectangles.SelectMany(r => new[] { r.BottomLeft.Y, r.TopRight.Y }).Distinct().OrderBy(x => x).ToList();
        var map = new int[ys.Count, xs.Count];
        var solution = new MapSolution(xs, ys, map);
        for (var i = 0; i < xs.Count; i++) solution._xToIndex[xs[i]] = i;
        for (var i = 0; i < ys.Count; i++) solution._yToIndex[ys[i]] = i;
        foreach (var rectangle in rectangles)
            for (var y = solution._yToIndex[rectangle.BottomLeft.Y]; y < solution._yToIndex[rectangle.TopRight.Y]; y++)
            for (var x = solution._xToIndex[rectangle.BottomLeft.X]; x < solution._xToIndex[rectangle.TopRight.X]; x++)
                map[y, x]++;
        return solution;
    }

    public int Query(Point point) => _map[_ys.RBinarySearch(point.Y), _xs.RBinarySearch(point.X)];
}

public static class ListExtensions
{
    public static int RBinarySearch(this List<int> arr, int target)
    {
        var (left, right) = (0, arr.Count - 1);
        while (left <= right)
        {
            var mid = (left + right) / 2;
            if (arr[mid] == target) return mid;
            if (arr[mid] > target) right = mid - 1;
            else left = mid + 1;
        }

        return right;
    }
}

public class SegmentTreeSolution : ISolution
{
    private readonly List<int> _xs, _ys;
    private readonly Dictionary<int, int> _xToIndex = new(), _yToIndex = new();
    private TreeNode?[] _roots = null!;

    private SegmentTreeSolution(List<int> xs, List<int> ys)
        => (_xs, _ys) = (xs, ys);

    public static ISolution Precompute(IReadOnlyCollection<Rectangle> rectangles)
    {
        var xs = rectangles.SelectMany(r => new[] { r.BottomLeft.X, r.TopRight.X }).Distinct().OrderBy(x => x).ToList();
        var ys = rectangles.SelectMany(r => new[] { r.BottomLeft.Y, r.TopRight.Y }).Distinct().OrderBy(x => x).ToList();
        var solution = new SegmentTreeSolution(xs, ys);
        if (rectangles.Count == 0)
            return solution;
        var xIndex = solution._xToIndex;
        var yIndex = solution._yToIndex;
        for (var i = 0; i < xs.Count; i++) xIndex[xs[i]] = i;
        for (var i = 0; i < ys.Count; i++) yIndex[ys[i]] = i;

        var operations = rectangles
            .SelectMany(r =>
                new[]
                {
                    new Operation(xIndex[r.BottomLeft.X], yIndex[r.BottomLeft.Y], yIndex[r.TopRight.Y] - 1, 1),
                    new Operation(xIndex[r.TopRight.X], yIndex[r.BottomLeft.Y], yIndex[r.TopRight.Y] - 1, -1)
                }
            )
            .OrderBy(e => e.X)
            .ToList();
        
        var roots = new TreeNode?[operations.Count * 2 + 1];
        var root = BuildTree(ys.Count);
        var lastX = operations[0].X;
        var c = 0;
        foreach (var operation in operations)
        {
            if (operation.X != lastX)
            {
                roots[c++] = root;
                lastX = operation.X;
            }

            root = Add(root, operation);
        }

        solution._roots = roots;
        return solution;
    }

    private static TreeNode? Add(TreeNode? root, Operation operation)
    {
        if (root is null) return null;
        if (operation.StartY <= root.LeftIndex && root.RightIndex <= operation.EndY)
            return root with { Value = root.Value + operation.Change };
        if (root.RightIndex < operation.StartY || operation.EndY < root.LeftIndex)
            return root;
        return root with
        {
            Left = Add(root.Left, operation),
            Right = Add(root.Right, operation)
        };
    }

    private static TreeNode BuildTree(int size)
    {
        return BuildTreeInternal(Enumerable.Repeat(0, 2 * size + 1).ToArray(), 0, 2 * size);

        TreeNode BuildTreeInternal(int[] array, int begin, int end)
        {
            if (begin >= end)
                return new TreeNode(array[begin], begin, end, null, null);
            var mid = (begin + end) / 2;
            var left = BuildTreeInternal(array, begin, mid);
            var right = BuildTreeInternal(array, mid + 1, end);
            return new TreeNode(left.Value + right.Value, left.LeftIndex, right.RightIndex, left, right);
        }
    }
    
    public int Query(Point point)
    {
        if (_xs.Count == 0)
            return 0;
        var xPos = _xs.RBinarySearch(point.X);
        var yPos = _ys.RBinarySearch(point.Y);
        if (xPos == -1 || yPos == -1)
            return 0;
        return QueryInternal(_roots[xPos], yPos);

        int QueryInternal(TreeNode? root, int y)
        {
            if (root is null)
                return 0;
            var mid = (root.LeftIndex + root.RightIndex) / 2;
            return y <= mid ?
                QueryInternal(root.Left, y) + root.Value :
                QueryInternal(root.Right, y) + root.Value;
        }
    }
}

public readonly record struct Operation(int X, int StartY, int EndY, int Change);

public record TreeNode(int Value, int LeftIndex, int RightIndex, TreeNode? Left, TreeNode? Right);

public interface ISolution
{
    static ISolution Precompute(IReadOnlyCollection<Rectangle> rectangles) => throw new NotImplementedException();
    int Query(Point point);
}

public readonly record struct Point(int X, int Y);

public readonly record struct Rectangle(Point BottomLeft, Point TopRight)
{
    public bool Contains(Point point) =>
        point.X >= BottomLeft.X && point.X < TopRight.X &&
        point.Y >= BottomLeft.Y && point.Y < TopRight.Y;
}