using Manifest.Data;
using Manifest.Services;

namespace Manifest.Tools;

/// <summary>
/// A print no card source lists, added from the command line - the same as the
/// card viewer's "Add a print" button, for the machine the server runs on.
/// </summary>
public static class PrintAdmin
{
    const string Usage = """
        manifest print add <card-id> --name NAME [--set SET] [--rarity R] [--price GBP] [--photo FILE]

          <card-id>    any printing of the card, e.g. EB02-003
          --name       what makes this print different, e.g. "CHOPPER's book promo"
          --set        the set it is filed under (default: Unnumbered Promos)
          --rarity     defaults to the card's own
          --price      a price in pounds, kept until you change it
          --photo      a JPEG, PNG or WebP of the card
        """;

    public static async Task<int> Run(string[] args, AppPaths paths)
    {
        if (args.Length < 2 || args[0] != "add")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 || args[0] is "-h" or "--help" ? 0 : 2;
        }

        string? name = null, set = "Unnumbered Promos", rarity = null, photoPath = null;
        double? price = null;
        for (var i = 2; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--name": name = Next(); break;
                case "--set": set = Next(); break;
                case "--rarity": rarity = Next(); break;
                case "--price": price = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
                case "--photo": photoPath = Next(); break;
                case "--root": Next(); break;
                default:
                    Console.Error.WriteLine($"unknown option {args[i]}\n\n{Usage}");
                    return 2;
            }
        }

        byte[]? photo = null;
        if (photoPath is not null)
        {
            photo = await File.ReadAllBytesAsync(photoPath);
            if (ImageCache.ContentTypeOf(photo) is null)
            {
                Console.Error.WriteLine($"{photoPath} is not a JPEG, PNG or WebP picture.");
                return 1;
            }
        }

        var database = new Database(paths);
        if (database.Initialise(false) is { } error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        string id;
        try
        {
            // Filed as the owner's: the person at the server's own command line.
            id = new CustomPrintRepository(database).Create(1, new CustomPrintInput(args[1], name ?? "", set, rarity, price));
        }
        catch (CustomPrintError e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }

        if (photo is not null)
        {
            var config = new AppConfig { Root = paths.Root };
            IImageStore store = config.ObjectStorageConfigured ? new S3ImageStore(config) : new LocalImageStore(paths);
            await store.Put(ImageCache.KeyFor(id), photo);
        }
        Console.WriteLine($"added {id}");
        return 0;
    }
}
