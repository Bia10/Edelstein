using System.Collections.Immutable;
using Edelstein.Plugin.Rue.Configs;
using Edelstein.Protocol.Gameplay.Login;
using Edelstein.Protocol.Gameplay.Login.Contexts;
using Edelstein.Protocol.Gameplay.Login.Contracts;
using Edelstein.Protocol.Gameplay.Login.Types;
using Edelstein.Protocol.Gameplay.Models.Characters;
using Edelstein.Protocol.Utilities.Pipelines;
using Microsoft.Extensions.Logging;

namespace Edelstein.Plugin.Rue.Plugs;

/// <summary>
/// Automatically selects character and enters game after world selection when auto-login is enabled.
/// Supports auto-creation of characters when none exist.
/// </summary>
public class UserOnPacketSelectWorldAutoSelectCharacterPlug : IPipelinePlug<UserOnPacketSelectWorld>
{
    private readonly ILogger? _logger;
    private readonly RueConfigLogin? _config;
    private readonly LoginContext _context;
    private readonly ICharacterRepository _characterRepository;

    public UserOnPacketSelectWorldAutoSelectCharacterPlug(
        ILogger? logger,
        RueConfigLogin? config,
        LoginContext context,
        ICharacterRepository characterRepository)
    {
        _logger = logger;
        _config = config;
        _context = context;
        _characterRepository = characterRepository;
    }

    public async Task Handle(IPipelineContext ctx, UserOnPacketSelectWorld message)
    {
        if (ctx.IsRequestedCancellation)
            return;

        if (!(_config?.IsAutoLogin ?? false))
            return;

        // Only proceed if world selection was successful and state transitioned to SelectCharacter
        if (message.User.State != LoginState.SelectCharacter)
            return;

        if (message.User.AccountWorld == null)
            return;

        // Get the character list
        var characters = (await _characterRepository.RetrieveAllByAccountWorld(message.User.AccountWorld.ID))
            .ToImmutableArray();

        ICharacter? selectedCharacter = null;

        // If no characters exist and auto-create is enabled, create one
        if (characters.Length == 0)
        {
            if (_config.AutoCreateCharacter && _config.AutoCharacterConfig != null)
            {
                selectedCharacter = await CreateAutoCharacter(message.User);
            }

            if (selectedCharacter == null)
            {
                _logger?.LogWarning("No characters found and auto-create is disabled or failed for user {Username}",
                    message.User.Account?.Username);
                return;
            }
        }
        else
        {
            // Find character by name or index
            selectedCharacter = FindCharacter(characters);
        }

        if (selectedCharacter == null)
        {
            _logger?.LogWarning("Could not find character to auto-select for user {Username}",
                message.User.Account?.Username);
            return;
        }

        _logger?.LogInformation(
            "Auto-selecting character {CharacterName} (ID: {CharacterID}) for user {Username}",
            selectedCharacter.Name,
            selectedCharacter.ID,
            message.User.Account?.Username
        );

        // Determine if we need to enable SPW or check existing SPW
        var hasSPW = !string.IsNullOrEmpty(message.User.Account?.SPW);
        var autoSPW = _config.AutoSPW ?? "0000"; // Default SPW if not configured

        if (hasSPW)
        {
            // Account has SPW, use CheckSPWRequest
            await _context.Pipelines.UserOnPacketCheckSPWRequest.Process(new UserOnPacketCheckSPWRequest(
                message.User,
                autoSPW,
                selectedCharacter.ID,
                "00-00-00-00-00-00",
                "00-00-00-00-00-00_00000000"
            ));
        }
        else
        {
            // Account doesn't have SPW, use EnableSPWRequest to set it and enter game
            await _context.Pipelines.UserOnPacketEnableSPWRequest.Process(new UserOnPacketEnableSPWRequest(
                message.User,
                selectedCharacter.ID,
                "00-00-00-00-00-00",
                "00-00-00-00-00-00_00000000",
                autoSPW
            ));
        }
    }

    private ICharacter? FindCharacter(ImmutableArray<ICharacter> characters)
    {
        // Try to find by name first
        if (!string.IsNullOrEmpty(_config?.AutoSelectCharacterName))
        {
            var byName = characters.FirstOrDefault(c =>
                c.Name.Equals(_config.AutoSelectCharacterName, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName;
        }

        // Then try by index
        if (_config?.AutoSelectCharacterIndex != null)
        {
            var index = _config.AutoSelectCharacterIndex.Value;
            if (index >= 0 && index < characters.Length)
                return characters[index];
        }

        // Default to first character
        return characters.FirstOrDefault();
    }

    private async Task<ICharacter?> CreateAutoCharacter(ILoginStageUser user)
    {
        var config = _config!.AutoCharacterConfig!;
        var baseName = config.NamePrefix;
        var name = baseName;
        var suffix = 1;

        // Find a unique name
        while (await _characterRepository.CheckExistsByName(name))
        {
            name = $"{baseName}{suffix}";
            suffix++;

            if (suffix > 9999) // Safety limit
            {
                _logger?.LogError("Could not find unique name for auto-created character with prefix {Prefix}", baseName);
                return null;
            }
        }

        _logger?.LogInformation("Auto-creating character {Name} for user {Username}",
            name, user.Account?.Username);

        // Trigger character creation pipeline
        var createResult = await _context.Pipelines.UserOnPacketCreateNewCharacter.Process(
            new UserOnPacketCreateNewCharacter(
                user,
                name,
                (RaceSelectType)config.Race,
                config.SubJob,
                config.Face,
                config.Hair,
                config.HairColor,
                config.Skin,
                config.Coat,
                config.Pants,
                config.Shoes,
                config.Weapon,
                config.Gender
            ));

        if (createResult.IsRequestedCancellation)
        {
            _logger?.LogWarning("Character creation was cancelled for user {Username}", user.Account?.Username);
            return null;
        }

        // Retrieve the newly created character
        return await _characterRepository.RetrieveByName(name);
    }
}
