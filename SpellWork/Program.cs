using SpellWork.Forms;
using SpellWork.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace SpellWork
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var dbcPath = $"{Settings.Default.DbcPath}\\{Settings.Default.Locale}";
            if (!Directory.Exists(dbcPath))
            {
                MessageBox.Show($"Files in {Path.GetFullPath(dbcPath)} missing", @"Missing files", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var config = BuildConfig();

            try
            {
                DBC.DBC.LoadAsync(config, NullSpellWorkLogger.Instance).GetAwaiter().GetResult();

                // Refuse to start on an incomplete client data set. DBC.LoadAsync
                // no longer aborts the whole load when one .db2 is missing -- it
                // leaves that single store null and carries on, which is what the
                // headless MCP server wants (it reports the gap per store and
                // still answers from the forty-five that loaded). A GUI wants the
                // opposite: every spell view here is driven straight off
                // SpellInfoStore and the DBC stores, with no null checks and no
                // way to say "this column is blank because the file is missing",
                // so starting would show an operator an incomplete spell list
                // that looks exactly like a complete one.
                //
                // This is what the app did before that change, and strictly
                // better: the load used to throw on the first missing file, so
                // the dialog could only ever name one. This names all of them.
                //
                // Only on this path is UnloadedStores a complete inventory: the
                // load returned, so every store was attempted. The catch below
                // cannot assume that -- see ShowLoadFailed.
                if (StoppedOnIncompleteData(config))
                    return;

                Application.Run(new FormMain(config));
            }
            catch (DirectoryNotFoundException dnfe)
            {
                MessageBox.Show(dnfe.Message, @"Missing required DBC file!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (ArgumentException ae)
            {
                MessageBox.Show(ae.Message, @"DBC file has wrong structure!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                ShowLoadFailed(config, ex);
                MessageBox.Show(ex.ToString());
            }
        }

        /// <summary>
        /// Reports a load that threw. Shows the error <em>and</em> whichever
        /// <c>.db2</c> files are genuinely absent, because either one alone can
        /// be the useless half.
        /// </summary>
        private static void ShowLoadFailed(SpellWorkConfig config, Exception ex)
        {
            var directory = Path.GetFullPath($"{config.DbcPath}\\{config.Locale}");
            var causes = Causes(ex).ToList();

            // Only files that are really not there. UnloadedStores is a live
            // view of "holds no data", and on this path that is not the same
            // question: an exception out of the Parallel.ForEach stops it
            // scheduling the stores it had not started, so the list includes
            // files sitting on disk in perfect health. Measured, on a directory
            // with one unreadable .db2 and one holding another store's payload:
            // sixteen stores unloaded, every one of them present. Reporting
            // those as missing would send an operator hunting for files they
            // can see, and bury the line that says the client build is wrong --
            // worse than the bare exception this guard replaced.
            var unloaded = DBC.DBC.UnloadedStores;
            var absent = unloaded
                .Where(store => !File.Exists($"{config.DbcPath}\\{config.Locale}\\{store}.db2"))
                .ToList();

            var message =
                $"Loading client data from {directory} failed:\r\n\r\n" +
                string.Join("\r\n", causes.Select(cause => "    " + cause)) +
                "\r\n\r\nSpellWork did not start.";

            if (absent.Count > 0)
            {
                message +=
                    "\r\n\r\nThese files are absent from that directory:\r\n" +
                    FileList(absent);
            }

            // Never presented as an inventory. The load did not finish, so
            // "unloaded" here means "no data yet" and not "something is wrong
            // with this store" -- said as a count rather than a list precisely
            // so it cannot be read as a list of suspects.
            if (unloaded.Count > absent.Count)
            {
                message +=
                    $"\r\n\r\nThe load stopped before it finished. {unloaded.Count - absent.Count} " +
                    "further store(s) hold no data, but their files are present -- most were " +
                    "probably never read rather than broken.";
            }

            // A .db2 that does not match this build's structures surfaces as an
            // ArgumentException from DBC.cs's TargetInvocationException branch.
            // It never reaches the catch (ArgumentException) above, because
            // Parallel.ForEach delivers it as an AggregateException; matching on
            // the causes is what gets it the title it was always meant to have.
            var wrongStructure = Leaves(ex).Any(cause => cause is ArgumentException);

            MessageBox.Show(message,
                wrongStructure ? @"DBC file has wrong structure!" : @"SpellWork Error!",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Distinct messages worth showing, capped.
        /// </summary>
        private static IEnumerable<string> Causes(Exception ex)
        {
            const int shown = 5;

            return Leaves(ex).Select(cause => cause.Message).Distinct().Take(shown);
        }

        /// <summary>
        /// The exceptions that actually say something, unwrapped from the two
        /// wrappers this load path adds.
        /// <para>
        /// <see cref="Parallel.ForEach{T}(System.Collections.Generic.IEnumerable{T},Action{T})"/>
        /// wraps whatever its body threw, so an <see cref="AggregateException"/>
        /// reading "One or more errors occurred." is all a caller sees
        /// otherwise. <see cref="TargetInvocationException"/> comes from
        /// <see cref="Activator.CreateInstance(Type,object[])"/> building the
        /// <c>Storage&lt;T&gt;</c>, and its own message -- "Exception has been
        /// thrown by the target of an invocation." -- says even less than the
        /// aggregate's. Both were measured on a deliberately wrong client
        /// build; unwrapping is what turns that pair into "DB type ... is not
        /// supported!".
        /// </para>
        /// </summary>
        private static IEnumerable<Exception> Leaves(Exception ex)
        {
            switch (ex)
            {
                case AggregateException aggregate:
                    return aggregate.Flatten().InnerExceptions.SelectMany(Leaves);
                case TargetInvocationException invocation when invocation.InnerException != null:
                    return Leaves(invocation.InnerException);
                default:
                    return new[] { ex };
            }
        }

        /// <summary>
        /// Indented one-per-line file names, capped. A wrong <c>DbcPath</c>
        /// fails all forty-six at once, and a box listing every one of them is
        /// unreadable and buries the fact that the path itself is the problem.
        /// </summary>
        private static string FileList(IReadOnlyList<string> stores)
        {
            const int shown = 15;

            var names = string.Join("\r\n", stores.Take(shown).Select(store => $"    {store}.db2"));
            if (stores.Count > shown)
                names += $"\r\n    ... and {stores.Count - shown} more";

            return names;
        }

        /// <summary>
        /// Shows which <c>.db2</c> files did not load and returns true if the
        /// application must not start. False means the client data is complete
        /// and usable.
        /// </summary>
        private static bool StoppedOnIncompleteData(SpellWorkConfig config)
        {
            var unloaded = DBC.DBC.UnloadedStores;
            if (unloaded.Count == 0)
            {
                // Belt and braces for the one case a store list cannot state on
                // its own: every store loaded, yet no spell was built from them.
                // Nothing produces this today -- SpellInfoStore is empty only
                // when SpellName is null, which puts SpellName in the list above
                // -- but an empty spell list is the exact failure this guard
                // exists to keep off an operator's screen, so it is checked
                // rather than reasoned about.
                if (DBC.DBC.SpellInfoStore.Count > 0)
                    return false;

                MessageBox.Show(
                    $"No spells were loaded from {Path.GetFullPath($"{config.DbcPath}\\{config.Locale}")}.\r\n\r\n" +
                    @"Check the DBC path and locale in Settings.",
                    @"No spell data!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return true;
            }

            MessageBox.Show(
                $"These files could not be loaded from {Path.GetFullPath($"{config.DbcPath}\\{config.Locale}")}:\r\n\r\n" +
                FileList(unloaded) + "\r\n\r\n" +
                @"SpellWork will not start with incomplete client data, because it cannot tell you which parts of a spell are missing rather than empty.",
                $@"Missing required DBC files ({unloaded.Count})!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return true;
        }

        internal static SpellWorkConfig BuildConfig()
        {
            return new SpellWorkConfig
            {
                DbcPath = Settings.Default.DbcPath,
                GtPath = Settings.Default.GtPath,
                Locale = Settings.Default.Locale,
                HotfixCachePath = Settings.Default.HotfixCachePath,
                MySqlHost = Settings.Default.Host,
                MySqlPortOrPipe = Settings.Default.PortOrPipe,
                MySqlUser = Settings.Default.User,
                MySqlPassword = Settings.Default.Pass,
                WorldDatabase = Settings.Default.WorldDbName,
                UseDatabase = Settings.Default.UseDbConnect
            };
        }
    }
}
