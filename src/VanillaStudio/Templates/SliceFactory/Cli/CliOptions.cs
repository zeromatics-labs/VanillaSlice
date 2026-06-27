namespace {{RootNamespace}}.SliceFactory.Cli;

public class CliOptions
{
    public CliCommand Command { get; set; } = CliCommand.None;

    // Named slice flags — non-null means "generate this slice with this display name"
    public string? ListingName { get; set; }
    public string? FormName { get; set; }
    public string? ActionName { get; set; }
    public string? SelectListName { get; set; }

    // SelectList configuration (only meaningful when SelectListName is set)
    public string SelectListModelType { get; set; } = "SelectOption";
    public string SelectListDataType { get; set; } = "string";

    // Shared options
    public string? Namespace { get; set; }
    public string? DirectoryName { get; set; }
    public string PrimaryKeyType { get; set; } = "Guid";
    public bool Preview { get; set; } = false;

    // Regenerate command options
    public string? SliceId { get; set; }

    public bool ShowHelp { get; set; } = false;

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        if (args.Length == 0)
            return options;

        var i = 0;
        while (i < args.Length)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "generate": case "gen": case "g":
                    options.Command = CliCommand.Generate; break;
                case "regenerate": case "regen": case "r":
                    options.Command = CliCommand.Regenerate; break;
                case "regenerate-all": case "regen-all": case "ra":
                    options.Command = CliCommand.RegenerateAll; break;
                case "list": case "ls": case "l":
                    options.Command = CliCommand.List; break;
                case "remove": case "rm": case "delete":
                    options.Command = CliCommand.Remove; break;

                case "--listing": case "-l":
                    options.ListingName = GetNextArg(args, ref i); break;
                case "--form": case "-f":
                    options.FormName = GetNextArg(args, ref i); break;
                case "--action": case "-a":
                    options.ActionName = GetNextArg(args, ref i); break;
                case "--select-list": case "-s":
                    options.SelectListName = GetNextArg(args, ref i); break;

                case "--select-model":
                    options.SelectListModelType = GetNextArg(args, ref i) ?? "SelectOption"; break;
                case "--select-type":
                    options.SelectListDataType = GetNextArg(args, ref i) ?? "string"; break;

                case "--namespace": case "-n":
                    options.Namespace = GetNextArg(args, ref i); break;
                case "--directory": case "-d":
                    options.DirectoryName = GetNextArg(args, ref i); break;
                case "--pk": case "--primary-key":
                    options.PrimaryKeyType = GetNextArg(args, ref i) ?? "Guid"; break;
                case "--preview":
                    options.Preview = true; break;
                case "--id":
                    options.SliceId = GetNextArg(args, ref i); break;
                case "--help": case "-h": case "help": case "?":
                    options.ShowHelp = true; break;

                default:
                    if (options.Command == CliCommand.Regenerate &&
                        !arg.StartsWith("-") && string.IsNullOrEmpty(options.SliceId))
                        options.SliceId = args[i];
                    break;
            }

            i++;
        }

        return options;
    }

    private static string? GetNextArg(string[] args, ref int i)
    {
        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
        {
            i++;
            return args[i];
        }
        return null;
    }

    public (bool IsValid, string? ErrorMessage) ValidateForGenerate()
    {
        if (string.IsNullOrEmpty(Namespace))
            return (false, "Namespace is required. Use --namespace <name>");

        if (string.IsNullOrEmpty(DirectoryName))
            return (false, "Directory is required. Use --directory <path>");

        if (ListingName == null && FormName == null && ActionName == null && SelectListName == null)
            return (false, "At least one slice type must be specified: --listing, --form, --action, or --select-list");

        var validPkTypes = new[] { "string", "Guid", "int", "long" };
        if (!validPkTypes.Contains(PrimaryKeyType, StringComparer.OrdinalIgnoreCase))
            return (false, $"Invalid primary key type. Must be one of: {string.Join(", ", validPkTypes)}");

        return (true, null);
    }

    public static string GetHelpText() => """
        SliceFactory CLI - Generate vertical slice boilerplate code

        USAGE:
            dotnet run -- <command> [options]

        COMMANDS:
            generate, gen, g        Generate a new slice
            regenerate, regen, r    Regenerate a specific slice from manifest
            regenerate-all, ra      Regenerate all slices from manifest
            list, ls, l             List all slices in manifest
            remove, rm, delete      Remove a slice from manifest

        GENERATE OPTIONS:
            --listing, -l <name>    Generate listing slice (e.g. "Doctors")
            --form, -f <name>       Generate form slice (e.g. "Doctor Profile")
            --action, -a <name>     Generate action slice (e.g. "Disable Doctor")
            --select-list, -s <name> Generate select list slice (e.g. "Doctor Types")
            --namespace, -n <name>  Module namespace [required]
            --directory, -d <path>  Output directory relative to solution root [required]
            --pk <type>             Primary key type: Guid, string, int, long (default: Guid)
            --select-model <type>   SelectList model type: SelectOption, Custom
            --select-type <type>    SelectList data type (default: string)
            --preview               Preview files without generating

        REGENERATE OPTIONS:
            --id <slice-id>         Slice ID to regenerate (or pass as argument)

        EXAMPLES:
            # Generate listing + form for Doctors
            dotnet run -- generate --listing "Doctors" --form "Doctor Profile" \
                --namespace ZeroLegal.Doctors --directory Features/Doctors

            # Generate a discrete action slice
            dotnet run -- generate --action "Disable Doctor" \
                --namespace ZeroLegal.Doctors --directory Features/Doctors

            # Preview without generating
            dotnet run -- generate --listing "Doctors" \
                --namespace ZeroLegal.Doctors --directory Features/Doctors --preview

            # Regenerate a specific slice
            dotnet run -- regenerate zerolegal-doctors-features-doctors

            # List all slices
            dotnet run -- list
        """;
}

public enum CliCommand
{
    None,
    Generate,
    Regenerate,
    RegenerateAll,
    List,
    Remove
}
