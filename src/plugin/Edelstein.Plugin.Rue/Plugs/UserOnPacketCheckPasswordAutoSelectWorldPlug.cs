using Edelstein.Plugin.Rue.Configs;
using Edelstein.Protocol.Gameplay.Login;
using Edelstein.Protocol.Gameplay.Login.Contexts;
using Edelstein.Protocol.Gameplay.Login.Contracts;
using Edelstein.Protocol.Utilities.Pipelines;
using Microsoft.Extensions.Logging;

namespace Edelstein.Plugin.Rue.Plugs;

/// <summary>
/// Automatically selects world/channel after successful password check when auto-login is enabled.
/// </summary>
public class UserOnPacketCheckPasswordAutoSelectWorldPlug : IPipelinePlug<UserOnPacketCheckPassword>
{
    private readonly ILogger? _logger;
    private readonly RueConfigLogin? _config;
    private readonly LoginContext _context;

    public UserOnPacketCheckPasswordAutoSelectWorldPlug(ILogger? logger, RueConfigLogin? config, LoginContext context)
    {
        _logger = logger;
        _config = config;
        _context = context;
    }

    public async Task Handle(IPipelineContext ctx, UserOnPacketCheckPassword message)
    {
        if (ctx.IsRequestedCancellation)
            return;

        if (!(_config?.IsAutoLogin ?? false))
            return;

        if (_config?.AutoSelectWorldID == null || _config?.AutoSelectChannelID == null)
            return;

        // Only proceed if login was successful and state transitioned to SelectWorld
        if (message.User.State != LoginState.SelectWorld)
            return;

        _logger?.LogInformation(
            "Auto-selecting world {WorldID} channel {ChannelID} for user {Username}",
            _config.AutoSelectWorldID,
            _config.AutoSelectChannelID,
            message.Username
        );

        // Trigger the SelectWorld pipeline
        await _context.Pipelines.UserOnPacketSelectWorld.Process(new UserOnPacketSelectWorld(
            message.User,
            _config.AutoSelectWorldID.Value,
            _config.AutoSelectChannelID.Value
        ));
    }
}
