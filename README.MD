# SevenThree
SevenThree (v0.1) is a ham radio Discord bot. You can use SevenThree to lookup call signs, as well take practice tests with the technician, general, or extra class questions!

This is a very early revision of the bot, more features and changes will be coming!

# Invite to your server
If you'd like to try out the bot on your server, use this invite link: https://discordapp.com/oauth2/authorize?client_id=576992584602353684&scope=bot&permissions=52224

# Help!
### Start a gamified test in a Discord channel
This example will start a test for the tech class license with 15 questions.
If you do not specify the number of questions, it will default to 35.

```
+start tech 15
```

### Start a test for the general class license with 10 questions
```
+start general 10
```

### Start a private test (direct message to you) for the extra class license, with 20 questions
For this you can either direct message the bot:

```
+start extra 20
```

Or if you're in a Discord server's channel, use:
```
+start extra 20 private
```

### Stop a started test
```
+stop
```

### Lookup a callsign
```
+lookup <callsign>
```

