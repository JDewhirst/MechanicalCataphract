using MechanicalCataphract.Data;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Discord;
using MechanicalCataphract.Services.Calendar;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MechanicalCataphract.Services
{
    public class TimeAdvanceService : ITimeAdvanceService
    {
        private readonly WargameDbContext _context;
        private readonly IGameStateService _gameStateService;
        private readonly IArmyService _armyService;
        private readonly INavyService _navyService;
        private readonly IMessageService _messageService;
        private readonly IMapService _mapService;
        private readonly IPathfindingService _pathfindingService;
        private readonly ICommanderService _commanderService;
        private readonly ICoLocationChannelService _coLocationChannelService;
        private readonly IDiscordChannelManager _discordChannelManager;
        private readonly INewsService _newsService;
        private readonly IWeatherService _weatherService;
        private readonly ICalendarService _calendarService;

        public TimeAdvanceService(
            WargameDbContext context,
            IGameStateService gameStateService,
            IArmyService armyService,
            INavyService navyService,
            IMessageService messageService,
            IMapService mapService,
            IPathfindingService pathfindingService,
            ICommanderService commanderService,
            ICoLocationChannelService coLocationChannelService,
            IDiscordChannelManager discordChannelManager,
            INewsService newsService,
            IWeatherService weatherService,
            ICalendarService calendarService)
        {
            _context = context;
            _gameStateService = gameStateService;
            _armyService = armyService;
            _navyService = navyService;
            _messageService = messageService;
            _mapService = mapService;
            _pathfindingService = pathfindingService;
            _commanderService = commanderService;
            _coLocationChannelService = coLocationChannelService;
            _discordChannelManager = discordChannelManager;
            _newsService = newsService;
            _weatherService = weatherService;
            _calendarService = calendarService;
        }

        public async Task<TimeAdvanceResult> AdvanceTimeAsync(int hours)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Read and advance world-hour
                var gameState = await _gameStateService.GetGameStateAsync();
                long oldWorldHour = gameState.CurrentWorldHour;
                long newWorldHour = oldWorldHour + hours;
                gameState.CurrentWorldHour = newWorldHour;

                // 2. Process message movement
                var messagesMoved = await ProcessMessageMovementAsync();

                // 3. Process army movement
                var armiesMoved = await ProcessArmyMovementAsync(oldWorldHour);

                // 4. Process navy movement
                var naviesMoved = await ProcessNavyMovementAsync(oldWorldHour);

                // 5. Embarked armies follow their navies after navy movement.
                armiesMoved += await ProcessEmbarkedArmyMovementAsync();

                // 6. Process commander movement. Commanders following embarked armies will
                // snap to the army's post-navy-movement location here.
                var commandersMoved = await ProcessCommanderMovementAsync();

                // 7. Enforce co-location proximity
                var coLocationRemovals = await EnforceCoLocationProximityAsync();

                // 7. Process supply consumption
                //int armiesSupplied = 0;
                //if (_calendarService.CrossedHourOfDay(oldWorldHour, newWorldHour, GameRules.Current.Supply.DailyUsageHour))
                //{
                //    armiesSupplied = await ProcessAllArmyDailySupplyConsumptionAsync();
                //}

                // 8. Send daily army, navy, and scouting reports
                //if (_calendarService.CrossedHourOfDay(oldWorldHour, newWorldHour, GameRules.Current.Armies.DailyReportHour))
                //{
                //    await _discordChannelManager.SendAllArmyReportsAsync();
                //    await _discordChannelManager.SendAllNavyReportsAsync();
                //    await _discordChannelManager.SendAllScoutingReportsAsync();
                //}

                // 8. Process event deliveries (news/rumour spreading)
                int newsProcessed = await _newsService.ProcessEventDeliveriesAsync(newWorldHour);

                // 9. Update weather when daily trigger is crossed
                int weatherUpdated = 0;
                if (_calendarService.CrossedHourOfDay(oldWorldHour, newWorldHour, GameRules.Current.Weather.DailyUpdateHour))
                {
                    weatherUpdated = await _weatherService.UpdateDailyWeatherAsync(newWorldHour);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new TimeAdvanceResult
                {
                    Success = true,
                    FormattedTime = _calendarService.FormatDateTime(newWorldHour),
                    MessagesDelivered = messagesMoved,
                    ArmiesMoved = armiesMoved,
                    NaviesMoved = naviesMoved,
                    CommandersMoved = commandersMoved,
                    //ArmiesSupplied = armiesSupplied,
                    CoLocationRemovals = coLocationRemovals,
                    NewsProcessed = newsProcessed,
                    WeatherUpdated = weatherUpdated
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

        private async Task<int> ProcessArmyMovementAsync(long worldHour)
        {
            int armiesMoved = 0;
            var armies = await _armyService.GetAllAsync();
            foreach (Army army in armies)
            {
                if (army.NavyId != null)
                    continue;

                armiesMoved += await _pathfindingService.MoveArmy(army, 1, worldHour);
            }
            return armiesMoved;
        }

        private async Task<int> ProcessNavyMovementAsync(long worldHour)
        {
            int naviesMoved = 0;
            var navies = await _navyService.GetAllAsync();
            foreach (Navy navy in navies)
            {
                naviesMoved += await _pathfindingService.MoveNavy(navy, 1, worldHour);
            }
            return naviesMoved;
        }

        private async Task<int> ProcessEmbarkedArmyMovementAsync()
        {
            int armiesMoved = 0;
            var navies = await _navyService.GetAllAsync();
            var navyLocations = navies
                .Where(n => n.CoordinateQ != null && n.CoordinateR != null)
                .ToDictionary(n => n.Id, n => (n.CoordinateQ, n.CoordinateR));

            var armies = await _armyService.GetAllAsync();
            foreach (Army army in armies)
            {
                if (army.NavyId == null)
                    continue;

                if (!navyLocations.TryGetValue(army.NavyId.Value, out var location))
                    continue;

                if (army.CoordinateQ == location.CoordinateQ && army.CoordinateR == location.CoordinateR)
                    continue;

                army.CoordinateQ = location.CoordinateQ;
                army.CoordinateR = location.CoordinateR;
                await _armyService.UpdateAsync(army);
                armiesMoved++;
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
