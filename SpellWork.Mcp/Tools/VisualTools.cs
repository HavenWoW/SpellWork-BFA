using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace SpellWork.Mcp.Tools
{
    /// <summary>
    /// get_spell_visual_chain. The 8.3.7 chain (SpellXSpellVisual ->
    /// SpellVisualEvent -> SpellVisualKitEffect) has no 5.4.8 counterpart;
    /// the 5.4.8 chain is being ported and this tool reports that until it
    /// lands.
    /// </summary>
    [McpServerToolType]
    public sealed class VisualTools
    {
        private readonly SpellDataService _data;

        public VisualTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell_visual_chain")]
        [Description("The resolved visual chain for one spell. Not yet available for the 5.4.8 client: returns a not_implemented error.")]
        public Task<string> GetSpellVisualChain(int id) =>
            Task.FromResult(ToolResponse.Error("not_implemented", "Visual chain is being ported to 5.4.8 (Task 7)."));
    }
}
