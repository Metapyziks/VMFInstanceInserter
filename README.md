VMFInstanceInserter (VMFII)
===========================

Inserts instances into a VMF. I am the best at names.

	https://github.com/Metapyziks/VMFInstanceInserter

How to Use With Hammer
----------------------

This is how to make Hammer automatically run this tool when you compile a map.

1.	Obtain vmfii.exe and (optionally) entities.txt.
	You can get vmfii.exe by compiling it yourself from the github repo or
	downloading it. If you download it, make sure it is extracted somewhere that
	you can remember for later. The entitiy definition file is optional, and is
	used to ensure all entities function correctly after being placed in an
	instance. The file entities.txt must be in the same directory as vmfii.exe
	for the entity definitions to work.
	
2.	Launch Hammer and open the Run Map dialogue (File -> Run Map... or F9).
	Switch to Expert mode by clicking the button at the bottom left.
	
	On the left is a list of commands that hammer runs when it compiles a map.
	By default, these should be the compilers vbsp.exe, vvis.exe, and vrad.exe,
	then a command to copy the compiled map to your game's map folder, and
	finally an optional command to run the game your map is for.
	
	On the right is information about whichever command is selected.
	
	If you are
	unsure as to what is going on here, have a read of this:
		
		https://developer.valvesoftware.com/wiki/Compiling
	
	What we want to do is run vmfii.exe to insert any instances in our map
	before the map is given to vbsp.exe.
	
3.	Click the "New" button, which adds a new empty command to the bottom of the
	list. This command will be the call to vmfii.exe, so we want this command to
	be executed first. Click "Move Up" until it is at the top.
	
	On the right, click on Cmds. This will give you a list of the various
	possible command types. Click on "Executable". In the file selection
	dialogue that just popped up, find vmfii.exe.
	
	We need to tell vmfii.exe which map to work with, and where to save the new
	map when it is done. We can tell it this by giving it parameters. In the
	"Parameters" text box enter the following:
	
	    $path\$file.$ext $path\$file.temp.$ext
		
	Hammer will replace each word with the '$' prefix with a relevant piece of
	information, like "$file" is the name of the .vmf it is compiling. You may
	notice that we are telling vmfii.exe to save the fixed map with .temp.vmf
	as its extension. This is so your original map isn't overwritten.
	
4.	Select the second line, which should start with "$bsp_exe". Change the
	parameters to this:
	
	    -game $gamedir $path\$file.temp.$ext
		
	This is telling vbsp.exe to compile the fixed .vmf made by vmfii.exe.

5.	This step is completely optional. If you want you could tell hammer to
	delete the fixed map now, since it isn't used again. To do this, make a new
	command and move it up to be after the "$bsp_exe" one. Choose "Delete File"
	after clicking "Cmds", and set the parameters to be:
	
	    $path\$file.temp.$ext
	
6.	After vbsp.exe compiled the map it produced two files, one ending in .bsp
	and the other .temp.prt. The next step, vvis.exe, expects the second file
	to not have .temp in its name. So we need to rename that file to get rid
	of the .temp. However, Hammer will refuse to rename a file if a file
	already exists with the new name. We'll have to tell Hammer to try and
	delete the .prt made by the last compile if it exists, and then rename the
	new one.
	
	Add a new command, and move it to be before "$vis_exe". Choose "Delete File"
	as the command type, and set the parameters to:
	
	    $path\$file.prt
	
	Now add another command, and move it to be after the last one we added.
	Choose "Rename File", and set the parameters to:
	
	    $path\$file.temp.prt $path\$file.prt
	
7.	That's it. Now just hit "Go!" when you want to compile. You won't have to
	enter that stuff in again unless you are setting up a new installation of
	hammer.

Adding Entity Definitions
-------------------------

Some entities will have properties that involve either angles or positions that
need to be changed when the entity is inserted as an instance. To tell vmfii.exe
which properties to correct, edit entities.txt.

TODO: Explain the format
