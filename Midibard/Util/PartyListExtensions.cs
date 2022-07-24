﻿using System.Linq;
using Dalamud.Game.ClientState.Party;
using HSC.DalamudApi;

namespace HSC.Managers.Ipc;


/// <summary>
/// author akira045/Ori
/// </summary>
public static class PartyListExtensions
{
    public static PartyMember? GetMeAsPartyMember(this PartyList PartyList) => PartyList.FirstOrDefault(i => i.ContentId == (long)api.ClientState.LocalContentId);
    public static PartyMember? GetPartyLeader(this PartyList PartyList) => PartyList[(int)PartyList.PartyLeaderIndex];
    public static bool IsInParty(this PartyList PartyList) => PartyList.Any();
    public static bool IsPartyLeader(this PartyMember member) => member != null && member.ContentId == api.PartyList.GetPartyLeader()?.ContentId;
    public static bool IsPartyLeader(this PartyList PartyList) => (long)api.ClientState.LocalContentId == PartyList.GetPartyLeader()?.ContentId;
    public static PartyMember? GetPartyMemberFromCID(this long cid) => api.PartyList.FirstOrDefault(i => i.ContentId == cid);
}