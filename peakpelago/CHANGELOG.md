## v0.6.2
<ul>
    <li>Badges and their checks should now work in custom runs, including stat-based badges</li>
    <li>EnergyLink conversion values for the new Gloom/Citadel items</li>
    <li>Amulets, Scout's Honor and Scoutmaster's Soul can no longer be converted into EnergyLink energy</li>
    <li>Three new EnergyLink store bundles featuring the new items</li>
    <li>New option Item Spawns: when Item Sanity is off, every item with an Acquire check is added to the pool as a useful item that spawns in front of you when received (Scout's Honor excluded)</li>
    <li>The Scoutmaster's Soul only orbits you while you are in the Nadir if received</li>
    <li>Support fixes for the latest patch</li>
</ul>

## v0.6.1
<ul>
    <li>Bug fixes galore</li>
    <li>Logic improvements</li>
    <li>Gloom/Citadel support of newly added items & badges</li>
    <li>New traps</li>
    <li>Goals refactored to allow multiple goals</li>
    <li>Item Tracker also allows toggleable spawning items by clicking them if theyre unlocked</li>
    <li>DamageLink/DamageLinkGroup support</li>
    <li>DeathLinkGroup support</li>
    <li>KnockbackLink support</li>
    <li>Backpack moved with Fannypack into Progressive Pack</li>
</ul>

## v0.6.0
<ul>
    <li>Stamina sync improvements</li>
    <li>Hint tracking/sending improvements</li>
    <li>Logic improvements</li>
    <li>The crashed plane items won't appear if their Unlock hasn't been acquired</li>
</ul>

## v0.5.9
<ul>
    <li>Items being locked now an option in the yaml called ItemSanity</li>
    <li>Item Spawn randomization is now an option called LootSanity</li>
    <li>Experimental BreathLink Support: Complete loss of Stamina linked to other BreathLinked games</li>
    <li>Trap Queue system to reduce trap spam</li>
    <li>Connection UI moved into pause menu under Archipelago button</li>
    <li>Archipelago Settings in game now includes toggling DeathLink, TrapLink, RingLink/HardRingLink, BreathLink on the fly</li>
    <li>Small handful of new traps</li>
    <li>Milk & Idol invincibility fixed</li>
    <li>Some badge logic fixed</li>
    <li>Potential fix for Basketball checks</li>
    <li>In Game Items as Items severely reduced to reduce potential crashing. Instead theres a handful of filler items so not every item can be sent to you.</li>
    <li>Potential band aid to reload stamina upgrades on run start</li>
    <li>Logic overhaul to account for ItemSanity and LootSanity</li>
    <li>Happy Camper Badge added to multiplayer badge list so you can disable it</li>
    <li>In Game Item tracker in the Archipelago section in the ESC/Pause menu</li>
    <li>Item Unlocks able to be hinted in the tracker</li>
    <li>Potential fixes for multiplayer issues with RingLink & EnergyLink</li>
    <li>DeathLink Checkpoint will now check for a Checkpoint Flag first before falling back to the campfire<li>
    <li>Scorchberry check and item unlock added</li>
    <li>Progressive Mountain wasn't syncing on receive, just on player joining, should be fixed</li>
    <li>Parasol check and unlock now considers both variants</li>
</ul>

## v0.5.8
<ul>
    <li>Items are locked and won't spawn until they are first recieved through AP</li>
    <li>Some potential mitigation against crashing due to picking up items and releasing games</li>
    <li>Zoom Trap protections incase it gets stuck active</li>
    <li>AP Connection UI will now only show once hosting the game</li>
    <li>Campfires will show a hint to Progressive Mountain</li>
    <li>Potential fix for Daredevil Badge not triggering</li>
    <li>24 Karat Badge in logic fixed</li>
    <li>EnergyLink values rebalanced & EnergyLink improved thanks to Jack5</li>
    <li>All items have the same weight in the loot pools and all multiplayer only items skip the multiplayer check when spawning</li>
    <li>Afflictions will kill you with additional stamina bars enabled now</li>
</ul>

## v0.5.7
<ul>
    <li>Clamped RingLink/HardRingLink extra stamina gains</li>
    <li>Fixed mouse cursor not being confined to game window</li>
    <li>Config option to toggle DeathLink (assuming its enabled in the AP slot)</li>
    <li>Config option to spawn the last X items received from AP on the SHORE (Traps & Progressive items are ignored)</li>
    <li>Multiplayer only items have been added to the loot tables all the time</li>
    <li>Custom Trivia Trap has been added. View the GUIDE on Github for more info.</li>
</ul>

## v0.5.6
<ul>
    <li>Fixed checks for Tick, Airline Food, Pirate's Compass</li>
    <li>High Altitude, Speed Climber and Participation Badges were checking internal names which were different for these 3 only</li>
    <li>Item pickup check sending performance improved</li>
    <li>Item spawning performance improved</li>
    <li>Berrynana Peel checks</li>
    <li>Logic fixes for some Badges</li>
    <li>Redundancy check for Progressive Ascent to prevent accessing higher ascents than you should</li>
    <li>Fix for Progressive Mountain ignoring the campfire before THE KILN</li>
    <li>More Open X Luggage checks</li>
    <li>Fix for Calcium Intake Badge not working</li>
    <li>Fix for Toxicology Badge not working</li>
    <li>New goal for Reaching # Ascent AND Collecting # Badges</li>
</ul>

## v0.5.5
<ul>
    <li>fixed Progressive Mountain not syncing to joined player(s)</li>
    <li>fixed Progressive Stamina not resyncing properly on joined player(s)</li>
</ul>

### v0.5.4
<ul>
    <li>DeathLink would send out even if disabled</li>
    <li>Gust Trap would change based on biome of the day</li>
    <li>6 New Traps</li>
    <li>Options to disable 3 different groups of Badges (Multiplayer, Difficult and Biome Specific)<br/>Difficult will not remove 24 Karat Badge and Biome will not remove the Nomad, Forestry, Alpinist or Trailblazer badges</li>
    <li>State saving won't happen if you aren't the host</li>
    <li>Progressive Endurance: Each one reduces the rate you use up Stamina, allowing you to climb more for less!</li>
    <li>Progressive Mountain: Each one allows you to progress to the next biome. The campfire will not clear the fog if you don't have enough of these!</li>
    <li>Fixed Nomad II and Forestry II checks</li>
    <li>Checks for some missing items (Clusterberries, Kingberries, Napberry, Scoutmaster's Bugle, Tick, Cloud Fungus)</li>
    <li>Getting Checks for an Ascent level will reward all previous level checks as well</li>
    <li>Added progression logic for the Progression item</li>
</ul>

### v0.5.3
<ul>
    <li>hotfix for items sometimes not sending until the next run</li>
    <li>hotfix for progressive stamina bars not loading properly</li>
    <li>Sudden disconnects will attempt reconnection</li>
    <li>AP UI will reset if reconnect attempts fail showing the Connection UI</li>
</ul>

### v0.5.2
<ul>
    <li>Hotfix for Traps not working if TrapLink was disabled</li>
    <li>Hotfix for assetbundle loading when downloaded through a mod manager</li>
    <li>Luggage Checks Per Run now properly resets</li>
    <li>EnergyLink wasn't properly communicating with the server</li>
    <li>Item mapping changed to IDs for more consistent results</li>
    <li>Yeet Trap needed updating due to one of the later post ROOTS updates</li>
</ul>

### v0.5.1
<ul>
    <li>hotfix for Death Links breaking when players are still waking up on the SHORE</li>
    <li>hotfix for save state loading logic happening out of order</li>
</ul>

### v0.5.0
<ul>
    <li>A handful of new Traps</li>
    <li>Energy Link support</li>
    <li>Save state handling reworked</li>
    <li>Item Check fixes</li>
    <li>Badge Check fixes</li>
    <li>Ascent Unlocks changed to Progressive Ascent</li>
    <li>Ascent Completion checks wont populate higher than your Ascent Level for the Peak Reached Goal</li>
</ul>

### v0.4.9
<ul>
    <li>Updated for Archipelago 0.6.4's new manifest</li>
</ul>

### v0.4.8
<ul>
    <li>Fixed save state loading was happening too early resulting in mixed up Luggage Checks</li>
    <li>DeathLink Checkpoint behavior should now be working properly</li>
    <li>Fixed ROOTS Ascent badge checks weren't sending properly</li>
    <li>HardRingLink support for certain conditions</li>
</ul>

### v0.4.7
<ul>
    <li>Luggage checks working in multiplayer (Without the mod too)</li>
    <li>AP Messages can be seen by others with the mod</li>
    <li>Progressive stamina working with others with the mod</li>
    <li>Fixed affliction traps not working on other players</li>
    <li>Fixed Death Links not sending due to the ROOTS update</li>
    <li>Added Badge checks for the ROOTS update badges</li>
    <li>Added Acquire Item checks for ROOTS update items</li>
    <li>Removed the unused Acquire Campfire check</li>
    <li>Added some new traps related to the ROOTS update</li>
    <li>Added missing item check(s): Marshmallow</li>
    <li>AP UI will change when joining a server to be less obtrusive</li>
</ul>