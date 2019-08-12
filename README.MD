![logo](https://github.com/gngrninja/SevenThree/blob/master/media/73.png?raw=true)

# SevenThree
SevenThree (v0.1) is a ham radio Discord bot. You can use SevenThree to lookup call signs, as well take practice tests with the technician, general, or extra class question pools!

This is a very early revision of the bot, more features and changes will be coming!

# Invite to your server
If you'd like to try out the bot on your server, use this invite link: https://discordapp.com/oauth2/authorize?client_id=576992584602353684&scope=bot&permissions=52224

# Help!
### Start a gamified test in a Discord channel
This example will start a test for the tech class license with 4 questions.
If you do not specify the number of questions, it will default to 35.

```
+start tech 4
```

![start](https://github.com/gngrninja/SevenThree/blob/master/media/quiz_start.png?raw=true)

Guessing is simple, just guess the letter of the answer. For tests in Discord channels, incorrect guesses will cause other guesses to be ignored until the next question is presented.

![wrong](https://github.com/gngrninja/SevenThree/blob/master/media/wrong_answer.png?raw=true)

### Start a test for the general class license with 10 questions
```
+start general 10
```

### Start a private test (direct messaged to you) for the extra class license, with 20 questions
For this you can either direct message the bot:
```
+start extra 20
```
Or if you're in a Discord server's channel, use:
```
+start extra 20 private
```

![startpriv](https://github.com/gngrninja/SevenThree/blob/master/media/start_priv.png?raw=true)

When you guess wrong on a private test, it will proceed to the next question, as well as tell you which answer was correct.

![startpriv](https://github.com/gngrninja/SevenThree/blob/master/media/priv_wrong.png?raw=true)

### Stop a test
```
+stop
```

![startpriv](https://github.com/gngrninja/SevenThree/blob/master/media/stop_cmd.png?raw=true)

### Lookup a callsign
```
+lookup <callsign>
```

This command will lookup information from QRZ, and present it in Discord. If they have a profile image, it will be shown in the embed.

![lookup](https://github.com/gngrninja/SevenThree/blob/master/media/lookup_call.png?raw=true)


# Issues?
Feel free to open up an issue against this repository, or message me on my discord server: https://gngr.ninja/discord