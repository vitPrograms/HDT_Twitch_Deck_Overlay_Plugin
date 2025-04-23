# TwitchDeckOverlay

## Overview

**TwitchDeckOverlay** is a plugin for Hearthstone Deck Tracker (HDT) that allows players and streamers to view Hearthstone decks shared in Twitch chat directly in-game via an overlay. The plugin automatically parses deck codes from Twitch chat messages and displays them in a convenient interface where you can view deck details, copy deck codes, or remove decks from the list.

This plugin is perfect for:
- **Hearthstone streamers** who want to quickly view decks shared by their audience.
- **Players** watching streams who want to try out decks posted in chat.

⚠️ **Important**: The plugin currently uses fixed UI sizes, so it works best at a resolution of 1920x1080. On other resolutions (e.g., 2560x1440 or 4K), UI elements may appear too small, too large, or extend beyond the screen.

---

## Features

- **Automatic Deck Parsing from Twitch Chat**:
  - The plugin connects to a specified Twitch channel and scans chat messages for deck codes.
- **In-Game Overlay**:
  - Displays a list of decks over the Hearthstone window.
  - Each deck shows the author, class, game mode (Standard/Wild), and time added.
- **Deck Details**:
  - Expand a deck to see its card list, including mana cost, name, rarity, and card count.
  - Hover over the class name to view the Hero Power (if available).
  - Hover over a card to see its full image and components (if applicable).
- **Copy Deck Code**:
  - Click the "📋" button to copy a deck code to your clipboard and paste it into Hearthstone.
- **Remove Decks**:
  - Click "🗑️" to remove a deck from the list.
- **Drag-and-Drop Overlay**:
  - Drag the overlay by its header to position it anywhere on the screen.
- **Show/Hide Overlay**:
  - Click "−" or "+" to collapse/expand the overlay.
  - The overlay becomes semi-transparent when the mouse leaves and fully opaque on hover.

---

## System Requirements

- **Hearthstone Deck Tracker (HDT)**:
  - Install the latest version of HDT ([official website](https://hsdecktracker.net/)).
  - The plugin has been tested with HDT version 1.43.7 and above.
- **Operating System**:
  - Windows 10 or later.
- **Hearthstone**:
  - Hearthstone game installed.
- **Internet Connection**:
  - Required for connecting to Twitch and loading card images.

---

## Installation

1. **Download the Plugin**:
   - Download the latest version of the plugin (`TwitchDeckOverlay.dll`) from [https://github.com/vitPrograms/HDT_Twitch_Deck_Overlay_Plugin/releases/latest](#).
2. **Locate the HDT Plugins Folder**:
   - Open the folder where HDT is installed (usually `C:\Users\<Your_Name>\AppData\Local\HearthstoneDeckTracker`).
   - Navigate to the `Plugins` folder (create it if it doesn’t exist).
3. **Copy the Plugin**:
   - Place the `TwitchDeckOverlay.dll` file into the `Plugins` folder.
4. **Launch HDT**:
   - Open Hearthstone Deck Tracker.
   - Go to **Settings → Plugins** and ensure the `TwitchDeckOverlay` plugin is enabled.
5. **Restart HDT**:
   - If the plugin doesn’t appear, restart HDT.

![image](https://github.com/user-attachments/assets/fc7d2924-3626-41e7-9d78-7ec97b48ce1a)

---

## Usage

### 1. Launch the Plugin
- Start Hearthstone Deck Tracker.
- Enable the plugin in **Settings → Plugins** if it’s not already active.
- In-game (or on the HDT main screen), you’ll see an overlay with the title "TWITCH DECKS".

### 2. Connect to Twitch
- Open the plugin settings via HDT.
- Enter the channel name (e.g., `streamer_name`) and press Enter.
- The plugin will connect to the chat and start scanning for deck codes.

![image](https://github.com/user-attachments/assets/669fef77-9592-466c-99d6-58d7938603e6)

![image](https://github.com/user-attachments/assets/ab14a27d-ef94-4e12-9a0b-048fd3e7d95d)

![image](https://github.com/user-attachments/assets/029b19d9-28b1-47a6-91ed-2f1468230fad)

### 3. View Decks
- When a deck code appears in the chat, the plugin will add it to the list.
- Each deck displays:
  - The author’s name (from Twitch chat).
  - The deck’s class (e.g., Mage, Warrior).
  - The game mode (Standard/Wild).
  - The time it was added.
- Expand a deck by clicking its header to see the card list.

![image](https://github.com/user-attachments/assets/ca62021a-8749-43c1-bd42-4232081bc4ed)

### 4. View Card Details and Hero Power
- Hover over the class name (e.g., "Mage") to see the Hero Power in a popup.
- Hover over a card to view its full image. If the card has components (e.g., "Zilliax or E.T.C."), they’ll appear below the main image.

![image](https://github.com/user-attachments/assets/161a7062-aa47-4dd4-95dc-214d610f6e08)

![image](https://github.com/user-attachments/assets/5c4dde06-badb-4e8c-ada2-e7c68ef608c5)

### 5. Copy Deck Code
- Click the "📋" button next to a deck to copy its code.
- The code is copied to your clipboard—paste it into Hearthstone (Ctrl+V) to create the deck.

### 6. Remove a Deck
- Click "🗑️" to remove a deck from the list.

### 7. Move and Collapse the Overlay
- Drag the overlay by the "TWITCH DECKS" header to reposition it.
- Click "−" to collapse the overlay (only the header will remain).
- Click "+" to expand it back.

---

## Known Issues

- **Fixed UI Sizes**:
  - The plugin is designed with fixed sizes (width: 350 pixels, max height: 600 pixels), so it works best at 1920x1080 resolution.
  - On 2K (2560x1440) or 4K (3840x2160) screens, text and elements may appear too small or too large, and popups may extend beyond the screen.
  - Adaptive scaling is planned for future updates.

If you experience display issues, try:
1. Changing your screen resolution to 1920x1080.
2. Setting Windows scaling to 100%.
3. Moving the overlay closer to the center of the screen.

---

## Contributing

If you’re a developer and want to contribute to the project, follow these steps:

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/vitPrograms/HDT_Twitch_Deck_Overlay_Plugin.git
   ```
2. **Open the Project in Visual Studio**:
   - Open the `.sln` solution file in Visual Studio (2019 or later recommended).
3. **Install Dependencies**:
   - Ensure the Hearthstone Deck Tracker SDK is referenced in the project.
4. **Build the Project**:
   - Build the project in Visual Studio (Ctrl+Shift+B).
   - The compiled `TwitchDeckOverlay.dll` will appear in the `bin/Debug` or `bin/Release` folder.
5. **Submit a Pull Request**:
   - Fork the repository, make your changes, and submit a Pull Request.
