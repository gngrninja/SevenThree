![logo](https://github.com/gngrninja/SevenThree/blob/master/media/73.png?raw=true)

# SevenThree
SevenThree (v0.1) is a ham radio Discord bot. You can use SevenThree to lookup call signs, as well take practice tests with the technician, general, or extra class question pools!

This is a very early revision of the bot, more features and changes will be coming.

# Join the SevenThree Discord server!
Come say hi, and take a test or two. This server will be the place to go for updates and help with SevenThree.
https://discord.gg/EhvxYbx

# Invite to your server
If you'd like to try out the bot on your server, use this invite link: https://discordapp.com/oauth2/authorize?client_id=576992584602353684&scope=bot&permissions=52224

It will need permissions to read chat (so you can issue commands), post messages/embeds, and upload images(for questions that have diagrams associated with them).

# Help!
### Start a gamified test in a Discord channel
This example will start a test for the tech class license with 4 questions.
If you do not specify the number of questions, it will default to 35.

```
+start tech 4
```

![start](https://github.com/gngrninja/SevenThree/blob/master/media/quiz_start.png?raw=true)

### Test starting shortcuts
You can start a test with the defaults by using either:

```
+tech
```
or
```
+general
```
or
```
+extra
```

![tech](https://github.com/gngrninja/SevenThree/blob/master/media/tech_short.png?raw=true)

Guessing is simple, just guess the letter of the answer. For tests in Discord channels, incorrect guesses will cause other guesses to be ignored until the next question is presented.

![wrong](https://github.com/gngrninja/SevenThree/blob/master/media/wrong_answer.png?raw=true)

### Start a test with 10 questions lasting 15 seconds

```
+start general 10 15
```

![1015](https://github.com/gngrninja/SevenThree/blob/master/media/gen_1015.png?raw=true)

### Start a private test (direct messaged to you) for the extra class license, with 20 questions

For this you can either direct message the bot:
```
+start extra 20
```
Or if you're in a Discord server's channel, use (will start a private test with 20 questions, with 25 seconds between questions):
```
+start extra 20 25 private
```

![startpriv](https://github.com/gngrninja/SevenThree/blob/master/media/priv_start.png?raw=true)
![startpriv](https://github.com/gngrninja/SevenThree/blob/master/media/priv_start2.png?raw=true)

When you guess wrong on a private test, it will proceed to the next question, as well as tell you which answer was correct.

![privwrong](https://github.com/gngrninja/SevenThree/blob/master/media/priv_wrong.png?raw=true)

### Stop a test
```
+stop
```

![stopcmd](https://github.com/gngrninja/SevenThree/blob/master/media/stop_cmd.png?raw=true)

### Lookup a callsign
```
+lookup <callsign>
```

This command will lookup information from QRZ, and present it in Discord. If they have a profile image, it will be shown in the embed.

![lookup](https://github.com/gngrninja/SevenThree/blob/master/media/lookup_call.png?raw=true)

### Change command prefix
If you would like to change the prefix for this bot's commands, use:

```
+prefix .
```

After using this command, be sure to remember the prefix you changed it to!

# Issues / Feedback?
Feel free to open up an issue against this repository, or ask on the Discord server, here: https://discord.gg/EhvxYbx.
