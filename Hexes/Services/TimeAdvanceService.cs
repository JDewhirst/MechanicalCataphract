using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services
{
    public class TimeAdvanceService : ITimeAdvanceService
    {
        private readonly WargameDbContext _context;
        private readonly IGameStateService _gameStateService;
        private readonly IArmyService _armyService;
        private readonly IMessageService _messageService;
        private readonly IMapService _mapService;
        private readonly IPathfindingService _pathfindingService;
        // Inject other services as needed

        public TimeAdvanceService(
            WargameDbContext context,
            IGameStateService gameStateService,
            IArmyService armyService,
            IMessageService messageService,
            IMapService mapService,
            IPathfindingService pathfindingService)
        {
            _context = context;
            _gameStateService = gameStateService;
            _armyService = armyService;
            _messageService = messageService;
            _mapService = mapService;
            _pathfindingService = pathfindingService;
        }

        public async Task<TimeAdvanceResult> AdvanceTimeAsync(TimeSpan amount)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Update game time
                var gameState = await _gameStateService.GetGameStateAsync();
                var newTime = gameState.CurrentGameTime.Add(amount);
                gameState.CurrentGameTime = newTime;

                // 2. Process message movement (in order)
                var messagesMoved = await ProcessMessageMovementAsync(newTime);

                // 2. Process supply consumption
                int armiesSupplied = 0;
                if (newTime.Hour == gameState.SupplyUsageTime.Hours + 1)
                {
                    armiesSupplied = await ProcessAllArmyDailySupplyConsumptionAsync();
                }

                // 3. Future: weather, army movement, event spreading etc.

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new TimeAdvanceResult
                {
                    Success = true,
                    NewGameTime = newTime,
                    MessagesDelivered = messagesMoved,
                    ArmiesSupplied = armiesSupplied
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new TimeAdvanceResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        
        private async Task<int> ProcessAllArmyDailySupplyConsumptionAsync()
        {
            int armiesSupplied = 0;
            var armies = await _armyService.GetAllAsync();
            foreach (Army army in armies)
            {
                army.CarriedSupply -= await _armyService.GetDailySupplyConsumptionAsync(army.Id);
                armiesSupplied += 1;
            }

            return armiesSupplied;
        }

        private async Task<int> ProcessMessageMovementAsync(DateTime currentTime)
        {
            int messagesMoved = 0;
            var messages = await _messageService.GetAllAsync();
            foreach (Message message in messages)
            {
                messagesMoved += await _pathfindingService.Move(message, 1);
            }
            return messagesMoved;
        }

    }
}
