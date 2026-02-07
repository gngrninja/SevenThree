![logo](https://github.com/gngrninja/SevenThree/blob/master/media/73.png?raw=true)

# SevenThree

SevenThree is a Discord bot for ham radio enthusiasts. Practice for your FCC Technician, General, or Extra license exams with gamified quizzes, review missed questions with study tools, check real-time propagation via PSKReporter, look up callsigns on QRZ, and view current band conditions — all from Discord.

## Join the SevenThree Discord Server

Come say hi, take a practice test, or get help with the bot.
https://discord.gg/EhvxYbx

## Invite to Your Server

Use this invite link to add SevenThree to your server: https://discord.com/oauth2/authorize?client_id=576992584602353684&scope=bot+applications.commands&permissions=52288

The bot needs the `applications.commands` scope for slash commands, plus permissions to read messages, send messages/embeds, and upload files (for question diagrams).

## Features

### Exams

Start a practice exam with quick shortcuts or customize your session.

| Command | Description |
|---|---|
| `/tech` | Quick start a Technician exam with defaults |
| `/general` | Quick start a General exam with defaults |
| `/extra` | Quick start an Extra exam with defaults |
| `/quiz tech\|general\|extra` | Start an exam with options for pool, question count, delay, and mode |
| `/quiz start` | Start an exam by selecting a license type |
| `/quiz stop` | Stop the current exam |

- **Private mode** (default) — questions are sent via DM so only you see them
- **Public mode** — questions are posted to the channel for group participation
- **Multiple question pools** — choose a specific pool (current, upcoming, or expired) or let it default to the current pool
- **Button-based answering** — tap A, B, C, or D to answer; buttons color-code after answering (green = correct, red = incorrect)

### Study & Review

Review and strengthen weak areas after taking practice exams.

| Command | Description |
|---|---|
| `/study missed` | Flashcard review of questions you answered incorrectly |
| `/study weak` | Focus on questions you've missed multiple times |
| `/study retry` | Re-quiz yourself on missed questions |
| `/study stats` | View your performance statistics by topic |

All study commands support optional filtering by license type, question pool, and subelement.

### PSK Reporter

Real-time propagation and spot data from PSKReporter, with paginated results.

| Command | Description |
|---|---|
| `/psk spots <callsign>` | Who is hearing a callsign |
| `/psk hearing <callsign>` | What a callsign is hearing |
| `/psk propagation <callsign>` | Propagation statistics (bands, countries, modes, best DX) |
| `/psk band <band> [mode]` | Activity on a specific band |
| `/psk grid <grid>` | Spots from a Maidenhead grid square |

### QRZ Lookup

| Command | Description |
|---|---|
| `/qrz lookup <callsign>` | Look up a callsign on QRZ.com |
| `/qrz dxcc <entity>` | Look up DXCC entity information |

### Callsign

| Command | Description |
|---|---|
| `/callsign set <callsign>` | Associate your callsign with your Discord account |
| `/callsign get` | View your associated callsign |

### Band Conditions

| Command | Description |
|---|---|
| `/conditions` | View current HF band conditions |

### Help

| Command | Description |
|---|---|
| `/help` | Interactive help with a category dropdown menu |

### Server Settings

Server administrators can configure quiz behavior per channel.

| Command | Description |
|---|---|
| `/quizsettings setchannel <license>` | Restrict a channel to a specific license type |
| `/quizsettings unsetchannel` | Remove channel restriction |
| `/quizsettings clearafter` | Toggle clearing messages after a quiz ends |

## Issues / Feedback

Feel free to open an issue against this repository, or ask on the Discord server: https://discord.gg/EhvxYbx
