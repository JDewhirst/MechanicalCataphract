using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
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
        private readonly ICommanderService _commanderService;
        private readonly ICoLocationChannelService _coLocationChannelService;
        private readonly IDiscordChannelManager _discordChannelManager;

        public TimeAdvanceService(
            WargameDbContext context,
            IGameStateService gameStateService,
            IArmyService armyService,
            IMessageService messageService,
            IMapService mapService,
            IPathfindingService pathfindingService,
            ICommanderService commanderService,
            ICoLocationChannelService coLocationChannelService,
            IDiscordChannelManager discordChannelManager)
        {
            _context = context;
            _gameStateService = gameStateService;
            _armyService = armyService;
            _messageService = messageService;
            _mapService = mapService;
            _pathfindingService = pathfindingService;
            _commanderService = commanderService;
            _coLocationChannelService = coLocationChannelService;
            _discordChannelManager = discordChannelManager;
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
                var messagesMoved = await ProcessMessageMovementAsync();

                // 3. Process army movement
                var armiesMoved = await ProcessArmyMovementAsync();

                // 4. Process commander movement
                var commandersMoved = await ProcessCommanderMovementAsync();

                // 4b. Enforce co-location proximity (remove commanders who moved away)
                var coLocationRemovals = await EnforceCoLocationProximityAsync();

                // 5. Process supply consumption
                int armiesSupplied = 0;
                if (newTime.Hour == gameState.SupplyUsageTime.Hours + 1)
                {
                    armiesSupplied = await ProcessAllArmyDailySupplyConsumptionAsync();
                }

                // 6. Future: weather, event spreading etc.

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new TimeAdvanceResult
                {
                    Success = true,
                    NewGameTime = newTime,
                    MessagesDelivered = messagesMoved,
                    ArmiesMoved = armiesMoved,
                    CommandersMoved = commandersMoved,
                    ArmiesSupplied = armiesSupplied,
                    CoLocationRemovals = coLocationRemovals
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

        private async Task<int> ProcessMessageMovementAsync()
        {
            int messagesMoved = 0;
            var messages = await _messageService.GetAllAsync();
            foreach (Message message in messages)
            {
                messagesMoved += await _pathfindingService.MoveMessage(message, 1);
            }
            return messagesMoved;
        }

        private async Task<int> ProcessArmyMovementAsync()
        {
            int armiesMoved = 0;
            var armies = await _armyService.GetAllAsync();
            foreach (Army army in armies)
            {
                armiesMoved += await _pathfindingService.MoveArmy(army, 1);
            }
            return armiesMoved;
        }

        private async Task<int> ProcessCommanderMovementAsync()
        {
            int commandersMoved = 0;
            var commanders = await _commanderService.GetAllAsync();
            foreach (Commander commander in commanders)
            {
                if (commander.FollowingArmyId != null && commander.FollowingArmy != null)
                {
                    // Snap to followed army's (post-move) position
                    if (commander.CoordinateQ != commander.FollowingArmy.CoordinateQ
                        || commander.CoordinateR != commander.FollowingArmy.CoordinateR)
                    {
                        commander.CoordinateQ = commander.FollowingArmy.CoordinateQ;
                        commander.CoordinateR = commander.FollowingArmy.CoordinateR;
                        await _commanderService.UpdateAsync(commander);
                        commandersMoved++;
                    }
                }
                else
                {
                    commandersMoved += await _pathfindingService.MoveCommander(commander, 1);
                }
            }
            return commandersMoved;
        }

        private async Task<int> EnforceCoLocationProximityAsync()
        {
            int totalRemovals = 0;
            var commanders = await _commanderService.GetAllAsync();
            foreach (var commander in commanders)
            {
                var removed = await _coLocationChannelService.EnforceProximityAsync(commander);
                foreach (var channel in removed)
                {
                    await _discordChannelManager.OnCommanderRemovedFromCoLocationAsync(channel, commander);
                    totalRemovals++;
                }
            }
            return totalRemovals;
        }

    }
}
