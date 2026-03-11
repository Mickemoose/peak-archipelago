## v0.5.8
<ul>
    <li>Items are locked and won't spawn until they are first recieved through AP</li>
    <li>Some potential mitigation against crashing due to picking up items and releasing games</li>
    <li>Zoom Trap protections incase it gets stuck active</li>
    <li>AP Connection UI will now only show once hosting the game</li>
    <li>Campfires will show a hint to Progressive Mountain</li>
    <li>Potential fix for Daredevil Badge not triggering</li>
    <li>24 Karat Badge in logic fixed</li>
    <li>EnergyLink values rebalanced & EnergyLink slightly reworked & improved thanks to Jacks5</li>
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