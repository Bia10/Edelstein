using System.Threading.Tasks;
using CommandLine;
using Edelstein.Core.Scripting;
using Edelstein.Service.Game.Fields.Objects.User;

namespace Edelstein.Service.Game.Commands
{
    public abstract class AbstractScriptedCommand<T> : AbstractCommand<T>
        where T : DefaultCommandContext
    {
        private readonly IScriptManager _scriptManager;

        protected AbstractScriptedCommand(Parser parser, IScriptManager scriptManager) : base(parser)
            => _scriptManager = scriptManager;

        protected override async Task Run(FieldUser sender, T ctx)
        {
            var script = await _scriptManager.Build($"c{Name}");

            script.Register("user", sender);
            script.Register("ctx", ctx);
            await script.Run();
        }
    }
}