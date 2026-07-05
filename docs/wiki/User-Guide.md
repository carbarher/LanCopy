# User Guide

This guide explains the desktop app flow for everyday use.

## 1. Start on both computers

Open LanCopy on both computers. They must be on the same local network: the same Wi-Fi, the same router by cable, or the same Ethernet switch.

LanCopy shows your local address at the top as `My IP`. The other computer may appear automatically in `Connect to`.

## 2. Connect

Recommended flow:

1. Open the `Connect to` list.
2. Pick the other computer.
3. Press `Connect`.

If the other computer does not appear, open `Advanced`, type its IP address manually, and press `Connect`.

## 3. Browse files

The left side is this computer. The right side is the other computer.

On first use, both sides start at the disk list. After that, LanCopy remembers the last folders used with each computer.

Use:

- double-click a folder to enter it
- the up arrow to go to the parent folder
- the home button to return to a home/start location
- the star button for quick folders
- resizable columns to adjust Name, Size and Date

## 4. Send and receive

To send files to the other computer:

1. Select files or folders on the left.
2. Press `Send`.

To receive files from the other computer:

1. Select files or folders on the right.
2. Press `Receive`.

During a transfer you can pause, resume or cancel. If a transfer is interrupted, LanCopy can resume instead of starting from zero.

## 5. Chat

Press `Chat` to open the chat window. Incoming messages open the chat automatically.

- Enter adds a new line.
- The send button sends the message.
- Messages show who sent them.

## 6. Protected mode

`Protect my computer` is enabled by default. Keep it enabled for normal use.

Protected mode keeps dangerous actions away from normal users. It helps prevent accidental deletion, renaming or access to sensitive system folders.

Use `Allow more access...` only when you trust the other computer and need temporary broader browsing. LanCopy restores protection automatically after the selected time or when the app closes.

## 7. Advanced actions

Advanced contains options for manual IP, ports, profiles, trusted devices, diagnostics, sync and remote power.

Remote shutdown/restart is intentionally in Advanced and requires permission from the other computer.

## 8. If connection fails

Use `Can't see the other PC?` first. It checks the local network and explains likely causes.

Common fixes:

- both computers must be on the same local network
- allow LanCopy through the firewall on private networks
- guest/public Wi-Fi can block computers from seeing each other
- cable connections still need a router/switch or compatible network setup
- manual IP is available in Advanced if automatic discovery is blocked

Default ports:

- TCP `8742` for file transfer and chat
- UDP `8743` for discovery
