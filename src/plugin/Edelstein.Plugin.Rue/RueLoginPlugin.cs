using Edelstein.Plugin.Rue.Configs;
using Edelstein.Plugin.Rue.Plugs;
using Edelstein.Protocol.Gameplay.Login.Contexts;
using Edelstein.Protocol.Plugin;
using Edelstein.Protocol.Plugin.Login;
using Edelstein.Protocol.Utilities.Pipelines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edelstein.Plugin.Rue;

/// <summary>
/// Login server plugin providing auto-registration, auto-login, and auto-character features.
/// </summary>
public class RueLoginPlugin : ILoginPlugin
{
    public string ID => "RueLogin";

    private ILogger? Logger { get; set; }
    private RueConfigLogin? Config { get; set; }

    public Task OnInit(IPluginHost<LoginContext> host, LoginContext ctx)
    {
        Logger = host.Logger;
        Config = new RueConfigLogin();
        return Task.CompletedTask;
    }

    public Task OnStart(IPluginHost<LoginContext> host, LoginContext ctx)
    {
        host.Config.Bind(Config);
        
        ctx.Pipelines.UserOnPacketCreateSecurityHandle.Add(PipelinePriority.High, new UserOnPacketCreateSecurityHandleAutoRegisterPlug(
            Logger,
            Config,
            ctx
        ));
        ctx.Pipelines.UserOnPacketCheckPassword.Add(PipelinePriority.High, new UserOnPacketCheckPasswordAutoLoginPlug(
            Logger,
            Config,
            ctx
        ));
        ctx.Pipelines.UserOnPacketCheckPassword.Add(PipelinePriority.Highest, new UserOnPacketCheckPasswordFlippedPlug(
            Logger,
            Config,
            ctx
        ));

        // Auto-login: after successful password check, auto-select world
        ctx.Pipelines.UserOnPacketCheckPassword.Add(PipelinePriority.PostNormal, new UserOnPacketCheckPasswordAutoSelectWorldPlug(
            Logger,
            Config,
            ctx
        ));

        // Auto-login: after successful world selection, auto-select character and enter game
        ctx.Pipelines.UserOnPacketSelectWorld.Add(PipelinePriority.PostNormal, new UserOnPacketSelectWorldAutoSelectCharacterPlug(
            Logger,
            Config,
            ctx,
            ctx.Repositories.Character
        ));

        return Task.CompletedTask;
    }

    public Task OnStop()
        => Task.CompletedTask;
}
