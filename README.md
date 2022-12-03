DXX Retro
=========

Latest versions
---------------

* D1X-Retro - 1.4X6
* D2X-Retro - 1.4X3 for Windows, 1.3a for Mac

Windows 10 Performance Notice
----------------------------
If you are noticing long periods of time when your game locks up, this is likely due to the Windows 10 2017 Fall Creator's Update.  To resolve this, turn on "Disable fullscreen optimizations" in the properties dialog of your Retro .exe file.

![image](https://user-images.githubusercontent.com/498929/36879953-01acc5fa-1d7b-11e8-9e4b-b8a6d6e1927f.png)

Changelog
---------

<h3>1.4X6 - 1/16/2018</h3>

This experimental release is for D1X-Retro only.

* Coop
* * Don't respawn robots in coop.
* Demos
* * Fix rewind bug where rewinding through a frame with multiple damage sources would show damage and energy incorrectly.
* Mac
* * Switch from XCode to scons for building.  Note: message box tech is disabled for now, crashes will only display to the terminal or the log.  To see them, run Descent from Terminal using the command `./d1x-retro-1.4.X6.app/Contents/MacOS/d1x-rebirth`
* Observer
* * Don't lock to player 1's view while observing in coop.
* * Observers no longer disconnected when other observers connect or disconnect.
* * Observers no longer see themselves connect as an unpatched client when someone else connects to the game.
* * Observers will now be properly told that they can't observe a game that hasn't yet started.
* * You can now observe players directly in first and third person view.  Use Ctrl+1 through Ctrl+8 to observe players 1 through 8.  Use Ctrl+9 and Ctrol+0 to cycle back and forth through the connected players and free observing.  Use Ctrl+- for first person view and Ctrl+= for third person view.
* * Added the above options to the multiplayer F1 help menu, and streamlined the menu to only display relevent commands.
* * Documented the /noobs command (kick all observers) in the F1 help menu.
* * Shields and shield changes are now visible to observers.

<h3>1.4X5 and earlier</h3>

* For changes to 1.4X5 and prior, please see the source.
