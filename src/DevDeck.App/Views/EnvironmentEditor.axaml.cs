using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DevDeck.Core;

namespace DevDeck.App.Views
{
    /// <summary>
    ///  Edits the named sets of values that <c>{{name}}</c> resolves from.
    /// </summary>
    /// <remarks>
    ///  Built in code rather than bound, and that is a considered choice rather than a shortcut.
    ///  A variable row is not a plain record: flagging one as a secret moves its value out of the
    ///  settings file and into the vault, un-flagging it takes it back out of the vault, renaming
    ///  one moves the vault entry, and renaming the environment has to carry every one of its
    ///  secrets with it. Those are transitions rather than properties, and expressing them as
    ///  two-way bindings onto a plain Core type would mean the vault being written on a keystroke
    ///  by something that cannot say what it was doing.
    ///
    ///  Secrets are never read back into the box. The vault holds the value and the field says
    ///  only whether there is one, which keeps the promise the rest of the panel makes - that this
    ///  is not somewhere a token is displayed - and costs only that changing a secret means typing
    ///  it again rather than editing it.
    ///
    ///  There is no cancel. Everything here takes effect as it is typed, the vault writes
    ///  included, and a Cancel button that could not take a secret back out of the vault would be
    ///  lying about what it does.
    /// </remarks>
    internal partial class EnvironmentEditor : Window
    {
        /// <summary>The environments being edited, in the order the list shows them.</summary>
        private readonly ObservableCollection<HttpEnvironment> environments = [];

        /// <summary>Whose values are on the right, held by reference rather than looked up.</summary>
        private HttpEnvironment? editing;

        /// <summary>
        ///  The name that environment's secrets are currently filed under in the vault.
        /// </summary>
        /// <remarks>
        ///  A secret's vault entry is qualified by its environment's name, so a rename has to move
        ///  every one of them. Kept apart from the model's own name because by the time a rename
        ///  needs acting on, the model already has the new name on it and the old one is the only
        ///  thing that can find what was stored.
        /// </remarks>
        private string filedUnder = string.Empty;

        /// <summary>Set while the editor is filling controls in, so that does not count as edits.</summary>
        private bool filling;

        public EnvironmentEditor() => InitializeComponent();

        /// <summary>
        ///  Shows the editor over <paramref name="owner"/> and hands back the edited list.
        /// </summary>
        /// <remarks>
        ///  A fresh list rather than the caller's, so a caller holding an ObservableCollection can
        ///  replace its contents in one go and raise one change rather than a dozen - the picker
        ///  above the address bar rebuilds from it.
        /// </remarks>
        internal static async Task<List<HttpEnvironment>> Edit(
            Window owner,
            IReadOnlyList<HttpEnvironment> existing)
        {
            EnvironmentEditor editor = new();

            foreach (HttpEnvironment environment in existing)
            {
                editor.environments.Add(environment);
            }

            editor.Names.ItemsSource = editor.environments;
            editor.Note.Text = Strings.Text("EnvSecretNote");

            if (editor.environments.Count > 0)
            {
                editor.Names.SelectedIndex = 0;
            }

            await editor.ShowDialog(owner);

            // The last environment edited never had a selection change to settle it.
            editor.MoveSecrets();

            return [.. editor.environments];
        }

        private void AddEnvironment(object? sender, RoutedEventArgs e)
        {
            HttpEnvironment fresh = new() { Name = UnusedName() };

            environments.Add(fresh);
            Names.SelectedItem = fresh;

            // Straight into the name field: the name it arrived with is a placeholder, and
            // replacing it is the first thing anyone does.
            EnvironmentName.Focus();
            EnvironmentName.SelectAll();
        }

        /// <summary>A name nothing else is using, so two "New environment" rows cannot appear.</summary>
        private string UnusedName()
        {
            string Root = Strings.Text("EnvNewName");

            if (!environments.Any(environment => environment.Name == Root))
            {
                return Root;
            }

            for (int suffix = 2; ; suffix++)
            {
                string candidate = $"{Root} {suffix}";

                if (!environments.Any(environment => environment.Name == candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        ///  Removes an environment, and the secrets that belonged to it.
        /// </summary>
        /// <remarks>
        ///  The vault entries go with it. Leaving them would mean a token outliving the only thing
        ///  on screen that referred to it, with nothing left that could find or clear it.
        /// </remarks>
        private void RemoveEnvironment(object? sender, RoutedEventArgs e)
        {
            if (editing is not { } going)
            {
                return;
            }

            foreach (EnvironmentValue secret in going.Values.Where(value => value.IsSecret))
            {
                SecretVault.Instance.Remove(EnvironmentValue.VaultName(filedUnder, secret.Name));
            }

            int was = Names.SelectedIndex;

            // Cleared first so that the selection change this causes does not try to settle an
            // environment that is on its way out.
            editing = null;
            filedUnder = string.Empty;

            environments.Remove(going);

            Names.SelectedIndex = environments.Count == 0 ? -1 : Math.Min(was, environments.Count - 1);
        }

        private void EnvironmentChanged(object? sender, SelectionChangedEventArgs e)
        {
            MoveSecrets();

            filling = true;

            editing = Names.SelectedItem as HttpEnvironment;
            filedUnder = editing?.Name ?? string.Empty;

            EnvironmentName.Text = editing?.Name ?? string.Empty;
            EnvironmentName.IsEnabled = editing is not null;

            Fill();

            filling = false;
        }

        /// <summary>Keeps the model's name in step with the box as it is typed.</summary>
        /// <remarks>
        ///  Only the model. Redrawing the list row here would raise a replace on the collection,
        ///  which the ListBox answers with a selection change - and settling and refilling the
        ///  editor between two keystrokes takes the caret with it. The row catches up when focus
        ///  leaves, which is also when the secrets move.
        /// </remarks>
        private void NameEdited(object? sender, TextChangedEventArgs e)
        {
            if (filling || editing is null)
            {
                return;
            }

            editing.Name = EnvironmentName.Text ?? string.Empty;
        }

        /// <summary>Settles a rename once the user has finished typing it.</summary>
        private void NameDone(object? sender, RoutedEventArgs e)
        {
            if (editing is null)
            {
                return;
            }

            MoveSecrets();
            Redraw(editing);
        }

        /// <summary>
        ///  Carries the edited environment's secrets across a rename.
        /// </summary>
        /// <remarks>
        ///  Done when the field is left rather than on every keystroke: the vault name is built
        ///  from the environment's name, and migrating per character would file the same secret
        ///  under every prefix of the new name on the way to it.
        /// </remarks>
        private void MoveSecrets()
        {
            if (editing is not { } environment
                || filedUnder.Length == 0
                || environment.Name == filedUnder)
            {
                return;
            }

            foreach (EnvironmentValue secret in environment.Values.Where(value => value.IsSecret))
            {
                string from = EnvironmentValue.VaultName(filedUnder, secret.Name);

                if (SecretVault.Instance.Value(from) is { } held)
                {
                    SecretVault.Instance.Set(EnvironmentValue.VaultName(environment.Name, secret.Name), held);
                    SecretVault.Instance.Remove(from);
                }
            }

            filedUnder = environment.Name;
        }

        /// <summary>
        ///  Repaints one row of the list after its name changed underneath it.
        /// </summary>
        /// <remarks>
        ///  The list draws from the Core type, which raises nothing when its name is set. Replacing
        ///  the entry with itself is what makes the row redraw; cheaper than giving a serialised
        ///  type change notification for the sake of one label.
        /// </remarks>
        private void Redraw(HttpEnvironment environment)
        {
            int at = environments.IndexOf(environment);

            if (at < 0)
            {
                return;
            }

            filling = true;

            environments[at] = environment;
            Names.SelectedItem = environment;

            filling = false;
        }

        private void AddValue(object? sender, RoutedEventArgs e)
        {
            if (editing is not { } environment)
            {
                return;
            }

            EnvironmentValue fresh = new();

            environment.Values.Add(fresh);

            Values.Children.Add(Row(environment, fresh));
        }

        /// <summary>Redraws the value rows for the environment being edited.</summary>
        private void Fill()
        {
            Values.Children.Clear();

            if (editing is not { } environment)
            {
                return;
            }

            foreach (EnvironmentValue value in environment.Values)
            {
                Values.Children.Add(Row(environment, value));
            }
        }

        /// <summary>
        ///  One variable: a switch, its name, its value, the secret flag, and a way to remove it.
        /// </summary>
        /// <remarks>
        ///  Each control writes to the value it was built for, which is what lets the secret
        ///  transitions be written as the two-step operations they are rather than as bindings.
        ///  The environment is captured rather than read from the field, so a row that outlives a
        ///  selection change cannot write into whichever environment happens to be current.
        /// </remarks>
        private Control Row(HttpEnvironment environment, EnvironmentValue value)
        {
            Grid row = new()
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,150,*,Auto,Auto"),
                Margin = new Avalonia.Thickness(0, 0, 0, 2),
            };

            CheckBox enabled = new()
            {
                IsChecked = value.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 6, 0),
            };

            ToolTip.SetTip(enabled, Strings.Text("EnvUseThisValue"));
            enabled.IsCheckedChanged += (_, _) => value.Enabled = enabled.IsChecked == true;

            TextBox name = new()
            {
                Text = value.Name,
                PlaceholderText = Strings.Text("EnvName"),
                Margin = new Avalonia.Thickness(0, 0, 6, 0),
            };

            TextBox held = new()
            {
                PlaceholderText = Strings.Text("EnvValue"),
                Margin = new Avalonia.Thickness(0, 0, 6, 0),
            };

            ToggleButton secret = new()
            {
                Content = Strings.Text("EnvSecret"),
                IsChecked = value.IsSecret,
                FontSize = 10.5,
                Padding = new Avalonia.Thickness(8, 4),
                Margin = new Avalonia.Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Find("SteelRaised"),
                Foreground = Find("TextFaint"),
                BorderBrush = Find("Line"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(2),
            };

            ToolTip.SetTip(secret, Strings.Text("EnvKeepInVault"));

            Button remove = new()
            {
                Content = "✕",
                Background = Brushes.Transparent,
                BorderThickness = default,
                Foreground = Find("TextFaint"),
                Padding = new Avalonia.Thickness(7, 3),
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Set rather than bound: what the value box says depends on whether this is a secret
            // and, when it is, on whether the vault has anything under it yet.
            void Dress()
            {
                held.PasswordChar = value.IsSecret ? '•' : default;

                held.PlaceholderText = value.IsSecret
                    ? SecretVault.Instance.Has(EnvironmentValue.VaultName(environment.Name, value.Name))
                        ? Strings.Text("EnvStoredTypeToReplace")
                        : Strings.Text("EnvNotSet")
                    : Strings.Text("EnvValue");

                held.Text = value.IsSecret ? string.Empty : value.Value;

                secret.Foreground = value.IsSecret ? Find("Accent") : Find("TextFaint");
            }

            Dress();

            name.TextChanged += (_, _) =>
            {
                string was = value.Name;
                string now = name.Text ?? string.Empty;

                if (value.IsSecret && was.Trim().Length > 0)
                {
                    // A secret renamed is a secret moved, for the same reason a renamed
                    // environment is: the vault knows it by that pair of names and by nothing else.
                    string from = EnvironmentValue.VaultName(environment.Name, was);

                    if (SecretVault.Instance.Value(from) is { } carried)
                    {
                        SecretVault.Instance.Set(EnvironmentValue.VaultName(environment.Name, now), carried);
                        SecretVault.Instance.Remove(from);
                    }
                }

                value.Name = now;
            };

            held.TextChanged += (_, _) =>
            {
                string typed = held.Text ?? string.Empty;

                if (!value.IsSecret)
                {
                    value.Value = typed;

                    return;
                }

                // An empty box is not an instruction to clear the stored secret: it starts empty
                // on every visit by design, and reading that as a deletion would wipe the vault
                // entry of every secret the user merely looked at.
                if (typed.Length > 0)
                {
                    SecretVault.Instance.Set(EnvironmentValue.VaultName(environment.Name, value.Name), typed);
                }
            };

            secret.IsCheckedChanged += (_, _) =>
            {
                bool wanted = secret.IsChecked == true;

                if (wanted == value.IsSecret)
                {
                    return;
                }

                string vaultName = EnvironmentValue.VaultName(environment.Name, value.Name);

                if (wanted)
                {
                    // Whatever was typed in the clear moves into the vault, which is the order
                    // anyone actually does this in: paste the token, then think better of having
                    // it in a file. Clearing Value is what takes it out of settings.json.
                    if (value.Value.Length > 0)
                    {
                        SecretVault.Instance.Set(vaultName, value.Value);
                    }

                    value.Value = string.Empty;
                    value.IsSecret = true;
                }
                else
                {
                    // Coming back out, the value is not restored into the file. Un-flagging is a
                    // decision to stop protecting something, not an instruction to write it
                    // somewhere it will be backed up - so the vault entry goes and the box is left
                    // empty for the user to type a plain value into if that is what they meant.
                    SecretVault.Instance.Remove(vaultName);

                    value.IsSecret = false;
                    value.Value = string.Empty;
                }

                Dress();
            };

            remove.Click += (_, _) =>
            {
                if (value.IsSecret)
                {
                    SecretVault.Instance.Remove(EnvironmentValue.VaultName(environment.Name, value.Name));
                }

                environment.Values.Remove(value);
                Values.Children.Remove(row);
            };

            Grid.SetColumn(enabled, 0);
            Grid.SetColumn(name, 1);
            Grid.SetColumn(held, 2);
            Grid.SetColumn(secret, 3);
            Grid.SetColumn(remove, 4);

            row.Children.Add(enabled);
            row.Children.Add(name);
            row.Children.Add(held);
            row.Children.Add(secret);
            row.Children.Add(remove);

            return row;
        }

        /// <summary>A palette brush, or null where the theme has not been merged in yet.</summary>
        private IBrush? Find(string key) =>
            this.TryFindResource(key, out object? found) && found is IBrush brush ? brush : null;

        private void DoneClick(object? sender, RoutedEventArgs e) => Close();
    }
}
