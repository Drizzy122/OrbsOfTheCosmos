using UnityEngine;
using UnityEngine.UIElements;

namespace Platformer
{
    /// <summary>
    /// Drives the Abilities tab. The tree layout is authored in AbilityTreeUI.uxml
    /// (nodes named node-{Branch}-{Tier}); this controller binds AbilityNodeData
    /// onto them, applies state classes, and handles unlock clicks.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class AbilityTreeUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PanelRenderer document;
        [SerializeField] AbilityTree abilityTree;   // on the Player

        const int TiersPerBranch = 4;

        VisualElement uiRoot;   // cached from the reload callback — Refresh() needs it
        Label pointsLabel;
        Label footerName;
        Label footerDesc;

        void Reset() => document = GetComponent<PanelRenderer>();

        void OnEnable()
        {
            document.RegisterUIReloadCallback(OnUIReload);
            if (abilityTree != null) abilityTree.OnChanged += Refresh;
        }

        void OnDisable()
        {
            document.UnregisterUIReloadCallback(OnUIReload);
            if (abilityTree != null) abilityTree.OnChanged -= Refresh;
        }

        void OnUIReload(PanelRenderer _, VisualElement root)
        {
            uiRoot = root;
            pointsLabel = root.Q<Label>("ability-points");
            footerName = root.Q<Label>("ability-footer-name");
            footerDesc = root.Q<Label>("ability-footer-desc");

            BindNodes(root);
            Refresh();
        }

        void BindNodes(VisualElement root)
        {
            foreach (AbilityBranch branch in System.Enum.GetValues(typeof(AbilityBranch)))
            {
                for (int tier = 1; tier <= TiersPerBranch; tier++)
                {
                    var element = root.Q<VisualElement>($"node-{branch}-{tier}");
                    if (element == null) continue;

                    var node = AbilityDatabase.instance != null
                        ? AbilityDatabase.instance.FindByBranchTier(branch, tier)
                        : null;

                    if (node == null) continue;   // no data authored for this position yet

                    // Fill authored labels from the data asset.
                    var nameLabel = element.Q<Label>("node-name");
                    if (nameLabel != null) nameLabel.text = node.displayName;
                    var costLabel = element.Q<Label>("cost");
                    if (costLabel != null) costLabel.text = node.cost.ToString();
                    var icon = element.Q<VisualElement>("icon");
                    if (icon != null && node.icon != null)
                        icon.style.backgroundImage = new StyleBackground(node.icon);

                    var captured = node;
                    element.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (abilityTree != null && abilityTree.Unlock(captured))
                            ShowInFooter(captured);   // refresh footer state text after unlock
                    });
                    element.RegisterCallback<MouseEnterEvent>(_ => ShowInFooter(captured));
                }
            }
        }

        void ShowInFooter(AbilityNodeData node)
        {
            if (footerName != null)
            {
                string state = abilityTree == null ? ""
                    : abilityTree.IsUnlocked(node.id) ? "  —  UNLOCKED"
                    : abilityTree.CanUnlock(node) ? $"  —  COST {node.cost}"
                    : !abilityTree.PrerequisiteMet(node) ? "  —  LOCKED (unlock the previous ability)"
                    : $"  —  NEED {node.cost} POINT(S)";
                footerName.text = node.displayName + state;
            }
            if (footerDesc != null) footerDesc.text = node.description;
        }

        void Refresh()
        {
            if (uiRoot == null) return;

            if (pointsLabel != null && abilityTree != null)
                pointsLabel.text = $"SKILL POINTS  {abilityTree.SkillPoints}";

            var root = uiRoot;
            foreach (AbilityBranch branch in System.Enum.GetValues(typeof(AbilityBranch)))
            {
                for (int tier = 1; tier <= TiersPerBranch; tier++)
                {
                    var element = root.Q<VisualElement>($"node-{branch}-{tier}");
                    if (element == null) continue;

                    var node = AbilityDatabase.instance != null
                        ? AbilityDatabase.instance.FindByBranchTier(branch, tier)
                        : null;

                    element.RemoveFromClassList("ability-node--locked");
                    element.RemoveFromClassList("ability-node--available");
                    element.RemoveFromClassList("ability-node--unlocked");

                    if (node == null || abilityTree == null)
                    {
                        element.AddToClassList("ability-node--locked");
                        continue;
                    }

                    if (abilityTree.IsUnlocked(node.id))
                        element.AddToClassList("ability-node--unlocked");
                    else if (abilityTree.CanUnlock(node))
                        element.AddToClassList("ability-node--available");
                    else
                        element.AddToClassList("ability-node--locked");

                    // Connector below this node lights up once the node is unlocked.
                    var connector = root.Q<VisualElement>($"connector-{branch}-{tier}");
                    if (connector != null)
                    {
                        if (abilityTree.IsUnlocked(node.id))
                            connector.AddToClassList("ability-connector--unlocked");
                        else
                            connector.RemoveFromClassList("ability-connector--unlocked");
                    }
                }
            }
        }
    }
}
