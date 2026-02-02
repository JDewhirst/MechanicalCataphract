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
        public string? FailureReason { get; init; }
        
    }
}
