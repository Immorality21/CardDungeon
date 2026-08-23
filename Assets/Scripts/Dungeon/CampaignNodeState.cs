using System.Collections.Generic;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// A campaign node resolved against one save: what it looks like and what the player may do with
    /// it. Produced by <see cref="CampaignOps.GetStates"/>; the map screen renders these directly and
    /// makes no unlock decisions of its own.
    /// </summary>
    public class CampaignNodeState
    {
        public CampaignNodeEntry Node;
        public CampaignNodeStatus Status;

        /// <summary>True when clicking this node starts the run fresh.</summary>
        public bool CanStart;

        /// <summary>True when clicking this node resumes the run already in progress.</summary>
        public bool CanContinue;

        /// <summary>
        /// Display names of the prerequisites still missing, for the "Requires ..." line on a locked
        /// node. Empty unless <see cref="Status"/> is <see cref="CampaignNodeStatus.Locked"/>.
        /// </summary>
        public List<string> MissingRequirements = new List<string>();

        public bool IsVisible => Status != CampaignNodeStatus.Hidden;
    }
}
