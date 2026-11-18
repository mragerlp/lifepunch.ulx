// Run with the native run_csharp tool in a stopped, isolated private ULX editor.
// The fixture sets only client receipt state. It never sends a grant or modifies a balance.
if (!Application.IsEditor || Game.IsPlaying || Project.Current.Config.FullIdent != "lifepunch.ulxqa")
    throw new Exception("Use a stopped private ULX QA project.");
var menuType = typeof(LifePunch.DXRP.Addons.StaffMenu.StaffMenu);
var host = menuType.Assembly.GetType("LifePunch.DXRP.Addons.StaffMenu.StaffMenuHost", true);
var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
          | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
var state = host.GetProperty("MoneyGrantState", flags);
var request = host.GetField("_moneyGrantRequestId", flags);
var submission = host.GetProperty("SubmittedMoneyGrant", flags);
var scene = new Scene();
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
void Client(string method, params object[] args) => host.GetMethod(method, flags).Invoke(null, args);
void Run(object panel, string method, params object[] args) => menuType.GetMethod(method, flags).Invoke(panel, args);
object Field(object panel, string name) => menuType.GetField(name, flags).GetValue(panel);
object NewMenu() => new GameObject(true, "Offline grant receipt fixture").Components.Create<LifePunch.DXRP.Addons.StaffMenu.StaffMenu>();
Sandbox.UI.Panel Draw(object menu)
{
    var panel = ((PanelComponent)menu).Panel;
    typeof(Sandbox.UI.Panel).GetMethod("InternalRenderTree", flags).Invoke(panel, null);
    return panel;
}
bool Caption(Sandbox.UI.Panel panel, string text) => panel.Descendants.OfType<Sandbox.UI.Label>().Any(x => !x.IsDeleting && x.Text?.Trim() == text);
void Click(Sandbox.UI.Panel button) => typeof(Sandbox.UI.Panel).GetMethod("DispatchEventImmediate", flags)
    .Invoke(button, new object[] { new Sandbox.UI.MousePanelEvent("onclick", button, "mouseleft") });
var mounted = host.GetField("_instance", flags);
var previousState = state.GetValue(null);
if (mounted.GetValue(null) != null || previousState.ToString() != "Idle" || (Guid)request.GetValue(null) != Guid.Empty)
    throw new Exception("Use an empty editor session without a prior grant.");
try
{
    using var scope = scene.Push();
    foreach (var destination in new[] { "cash", "bank" })
    {
        foreach (var initialState in new[] { "Pending", "Unknown" })
        {
            var id = Guid.NewGuid();
            var snapshot = Activator.CreateInstance(submission.PropertyType, new object[] { 202L, "Test recipient", 50u, destination, "Fixture reason" });
            request.SetValue(null, id);
            submission.SetValue(null, snapshot);
            state.SetValue(null, Enum.Parse(state.PropertyType, initialState));
            var menu = NewMenu();
            var header = Draw(menu);
            var receiptButton = header.Descendants.Single(x => !x.IsDeleting && x.HasClass("header-chip") && Caption(x, "Grant receipt"));
            Click(receiptButton);
            Check(Field(menu, "_pendingAction") != null, "An unresolved receipt must reopen without a selected player.");
            var receiptPanel = Draw(menu);
            Check(Caption(receiptPanel, destination) && Caption(receiptPanel, "$50") && Caption(receiptPanel, "Test recipient")
                  && Caption(receiptPanel, "Fixture reason"), "Receipt did not render the frozen amount, destination, target and reason.");
            Check(!Caption(receiptPanel, "Confirm Give Money") && Caption(receiptPanel, "Dismiss receipt"), "Receipt exposed an editable confirmation.");
            Click(receiptPanel.Descendants.Single(x => !x.IsDeleting && x.HasClass("grant-close-btn")));
            Check(Field(menu, "_pendingAction") == null && (Guid)request.GetValue(null) == id, "Dismiss forfeited the receipt or blocked the form.");
            Run(menu, "SelectTab", "Commands");
            Check((string)Field(menu, "_activeTab") == "Commands", "Grant trapped staff navigation.");
            Run(menu, "Select", 303L);
            Check((long)Field(menu, "_profileSteamId") == 303L, "Receipt trapped roster or changed target authorization.");
            mounted.SetValue(null, menu);
            Click(Draw(menu).Descendants.Single(x => !x.IsDeleting && x.HasClass("chrome-close")));
            Check(mounted.GetValue(null) == null && (Guid)request.GetValue(null) == id, "Shell close failed to release the menu and preserve its grant.");
            var reopened = NewMenu();
            var action = LifePunch.DXRP.Addons.StaffMenu.StaffMenuActions.All.First(x => x.Key == "givemoney");
            Run(reopened, "BeginArgs", action);
            Check(Field(reopened, "_pendingAction") == action && Field(reopened, "_selectedSteamId") == null, "Reopened grant silently retargeted a command.");
            var same = submission.GetValue(null);
            Check(ReferenceEquals(same, snapshot) && (string)same.GetType().GetProperty("Destination").GetValue(same) == destination
                && (uint)same.GetType().GetProperty("Amount").GetValue(same) == 50u, "Reopened receipt lost its original payload.");
            Run(reopened, "ConfirmArgs", new LifePunch.DXRP.Addons.StaffMenu.StaffMenuPlayer());
            Check((Guid)request.GetValue(null) == id && !(bool)Field(reopened, "_severeConfirm"), "A read-only receipt armed a new payment.");
            Client("OnMoneyGrantReconcileState", id, false, "Unknown fixture");
            var unknownPanel = Draw(reopened);
            Click(unknownPanel.Descendants.Single(x => !x.IsDeleting && x.HasClass("grant-reconcile-btn")));
            // There is no payment bridge in the stopped isolated scene.
            Check(state.GetValue(null).ToString() == "Unknown" && (Guid)request.GetValue(null) == id, "Recheck reset the unresolved request.");
            Client("OnMoneyGrantResult", Guid.NewGuid(), true, "Foreign result");
            Check(state.GetValue(null).ToString() == "Unknown", "Wrong request resolved the receipt.");
            Client("OnMoneyGrantResult", id, true, "Verified fixture");
            Client("OnMoneyGrantReconcileState", id, false, "Earlier query arrived late");
            Check(state.GetValue(null).ToString() == "Succeeded", "Late reconciliation regressed a final receipt.");
            Run(reopened, "CancelArgs");
            Check(submission.GetValue(null) == null && (Guid)request.GetValue(null) == Guid.Empty, "Finished grant did not release the local form.");
        }
    }
    return new { exitCode = 0, checks, transfers = 0 };
}
finally
{
    mounted.SetValue(null, null);
    state.SetValue(null, Enum.Parse(state.PropertyType, "Idle"));
    Client("ClearMoneyGrantResult");
    scene.Destroy();
}
