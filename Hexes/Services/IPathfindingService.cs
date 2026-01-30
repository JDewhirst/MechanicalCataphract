using Hexes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services
{
    public interface IPathfindingService
    {
        Task<PathResult> FindPathAsync(Hex start, Hex end);
    }

    public class PathResult
    {
        public bool Success { get; init; }
        public IReadOnlyList<Hex> Path { get; init; } = Array.Empty<Hex>();
        public int TotalHexes { get; init; }        // Path length in hexes
        public int RoadHexes { get; init; }         // Hexes with road connection
        public int OffRoadHexes { get; init; }      // Hexes without road connection
        public string? FailureReason { get; init; }
    }
}
