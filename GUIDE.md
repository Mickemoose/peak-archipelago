![PEAKPELAGO](PeakPelagoLogo.png)

# PEAK Archipelago Mod

An Archipelago integration mod for the game PEAK, allowing the game to be played as part of a multiworld randomizer.

Also available on Thunderstore: https://thunderstore.io/c/peak/p/PeakArchipelago/PEAKPELAGO/

## Table of Contents

- [Overview](#overview)
- [Client Plugin Installation](#client-plugin-installation)
  - [Thunderstore Install](#thunderstore-install)
  - [Manual Install](#manual-install)
- [Archipelago World Installation](#archipelago-world-installation)
- [How to Play](#how-to-play)
- [Multiplayer](#multiplayer)
- [Links](#links)
  - [DeathLink](#deathlink)
  - [RingLink/HardRingLink](#ringlinkhardringlink)
  - [EnergyLink](#energylink)
  - [TrapLink](#traplink)
- [Custom Trivia Trap](#custom-trivia-trap)

## Overview

This mod connects PEAK to the [Archipelago](https://archipelago.gg/) multiworld randomizer system. Ascent unlocks, badges, and other progression items are randomized across multiple games and players, creating a unique cooperative or competitive experience.

## Client Plugin Installation

### Thunderstore Install
1. **Install Mod Loader/Manager**:
   - Download a mod manager like [Gale Mod Manager](https://thunderstore.io/c/peak/p/Kesomannen/GaleModManager/)

2. **Install the PEAKPELAGO Mod**:
   - Download [PEAKPELAGO Mod](https://thunderstore.io/c/peak/p/PeakArchipelago/PEAKPELAGO/)

3. **Launch the Game**:
   - Start PEAK - the plugin will create a configuration file on first run
   - Connect using the in game UI

### Manual Install
1. **Install BepInEx**:
   - Download BepInEx 5.x for your platform
   - Extract to your PEAK game directory
   - Run the game once to generate BepInEx folders

2. **Install the Plugin**:
   - Download the `peakpelago` folder from the releases
   - Drag the entire `peakpelago` folder into your `BepInEx/plugins/` directory
   - The folder contains all necessary files
   - If you have a PeakArcihpelagoPluginDLL folder still, delete it

3. **Launch the Game**:
   - Start PEAK - the plugin will create a configuration file on first run
   - Connect using the in game pause menu

## Archipelago World Installation

1. **Locate Archipelago Installation**:
   - Double click the peak.apworld file to install the PEAK AP World into your Archipelago installation

## How to Play

1. **Generate a Multiworld**:
   - Create a YAML configuration for your PEAK world
   - Generate the multiworld using Archipelago's generator
   - Host or join a multiworld session

2. **Start PEAK**:
   - Launch the game with the mod installed
   - If you're hosting, host a lobby, otherwise join someone.

3. **Connect to Archipelago**:
   - As the host, press Pause/ESC and click Archipelago.
   - Fill in the connection details and click Connect.
   - Optionally toggle some of the DeathLink/TrapLink/RingLink options if applicable.
   - During gameplay the Archipelago icon on the bottom left will be colored if connected, uncolored if not connected.

4. **Play the Game**:
   - Ascents are initially locked - unlock them by receiving items
   - Collecting items and completing objectives sends checks to other players
   - Receive some item unlocks or progressive items from other players as they complete their objectives
   - Work together (or compete) to complete your goals!

## Multiplayer

If you wish to climb the PEAK with your friends you can do so!
All players just need to download the PEAKPELAGO Mod for their PEAK Game and join you as they would normally.
The AP Connection UI will update to show that it's connected via the host

If someone joining the Host has their AP connection set to their own slot, anything connected to AP will likely only affect the Host's AP slot.

## Links

### DeathLink

Death Link has a few behaviors to choose from.

**Receiving Behavior:**
- Kill Random Player: A random player in your lobby will be killed
- Reset to Last Checkpoint: All players will be teleported to the last checkpoint/campfire

**Sending Behavior:**
- Any Player Dies: Send Death Link whenever any player in your game dies
- All Players Dead: Send Death Link only when all players are dead (game over)

### RingLink/HardRingLink

**RingLink**
With RingLink enabled, your stamina bars are conditionally linked to other RingLink games.
Consuming food will send Rings to other players with Ring Link enabled. Poisonous food will send negative rings.
Positive and Negative rings received will affect your Extra Stamina first, then your regular Stamina.

**HardRingLink**
With HardRingLink enabled, your stamina bars are conditionally linked to other HardRingLink games.
While rings received is largely unchanged, the rings you send out only happens on specific events.
Wrecking food while cooking, Causing the Scoutmaster to appear, Reaching the ends of Biomes, Dying and/or Zombifying will send out negative rings.

### EnergyLink

With EnergyLink enabled, an EnergyLink Vendor will appear at each campfire. Each one will have 1 randomized Item bundle inside that you can spend energy on to receive.
Any items you pick up will now have another interaction called Convert which will convert the item into energy, destroying it in the process.
Most items are 100J of energy, while some have unique values.

### TrapLink

With TrapLink enabled, any Traps you receive or find will send out to TrapLinked games that are able to accept them. When linked games do the same, you
will receive a linked trap as well based on the type of trap.

### BreathLink

With BreathLink enabled, any time a player loses all of their stamina other BreathLinked games will lose all of their stamina (or equivalent).
When a linked game sends a BreathLink, all players will lose all their stamina.


## Custom Trivia Trap

With the Custom Trivia Trap, you can set up your own Trivia Questions for you and your friends!
There are a handful of default questions so even if you don't want to set up your own you can still have some good old trivia fun.

### Configuration

Two config options are available in your BepInEx config:

| Option | Default | Description |
|--------|---------|-------------|
| `CustomTriviaFolder` | `plugins/PeakArchipelago-PEAKPELAGO/CustomTrivia` | Folder path for custom trivia questions (relative to BepInEx folder) |
| `IncludeStandardTrivia` | `true` | Whether to include the standard trivia questions along with custom ones |

### Creating Custom Questions

Create a `.yaml` or `.yml` file in your CustomTrivia folder. The mod will search recursively, so you can organize questions into subfolders.

Each question needs:
- `question`: The question text
- `correct_answer`: The correct answer (must be included in options)
- `options`: At least 4 possible answers (3 random wrong answers will be shown alongside the correct one)
- `timer`: (Optional) Time limit in seconds, defaults to 30
```yaml
questions:
  - question: "What is the largest country by area?"
    correct_answer: "Russia"
    options:
      - "Russia"
      - "Canada"
      - "China"
      - "United States"
      - "Brazil"
      - "Australia"
    timer: 20

  - question: "Which river is the longest in the world?"
    correct_answer: "Nile"
    options:
      - "Amazon"
      - "Nile"
      - "Yangtze"
      - "Mississippi"

  - question: "What is the capital of Japan?"
    correct_answer: "Tokyo"
    options:
      - "Seoul"
      - "Beijing"
      - "Tokyo"
      - "Bangkok"
      - "Osaka"
      - "Kyoto"
```

**Tip:** Add more than 4 options to increase variety! The mod will randomly pick 3 wrong answers each time the question appears.

