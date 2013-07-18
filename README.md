VMFInstanceInserter (VMFII)
===========================

Inserts instances into a VMF. I am the best at names.

	https://github.com/Metapyziks/VMFInstanceInserter

How to Use With Hammer
----------------------

This is how to make Hammer automatically run this tool when you compile a map.

1.	Obtain vmfii.exe.
	You can get vmfii.exe by compiling it yourself from the github repo or
	downloading it. You will need to put vmfii.exe in the bin/ directory of the
	source sdk branch you are using (the directory with vbsp.exe, vvis.exe etc).
	
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
	map when it is done. We can tell it this by giving it parameters. You can
	also specify which FGD files should be used to work out what to do with
	different entity classes. In the "Parameters" text box enter the following
	(replacing "first.fgd,second.fgd,..." with the absolute paths to any FGD files
	used by the mod you are mapping for):
	
	    $path\$file.$ext $path\$file.temp.$ext --fgd "first.fgd,second.fgd,..."
		
	Hammer will replace each word with the '$' prefix with a relevant piece of
	information, like "$file" is the name of the .vmf it is compiling. You may
	notice that we are telling vmfii.exe to save the fixed map with .temp.vmf
	as its extension. This is so your original map isn't overwritten.
	
4.	Select the second line, which should start with "$bsp_exe". Change the
	parameters to this:
	
	    -game $gamedir $path\$file.temp.$ext
		
	This is telling vbsp.exe to compile the fixed .vmf made by vmfii.exe.

5.	Now we need to prepare the output of vbsp.exe for vvis.exe. Make a new
	command after the second line, and set the command type to be "Executable".
	Now navigate to vmfii.exe again (it should already be in the right
	directory) but this time use these as the arguments:
	
		$path\$file.$ext $path\$file.temp.$ext --cleanup
		
	Specifically, this will delete the .temp.vmf that vmfii.exe created since
	it isn't needed again, and will rename the .temp.prt file vbsp.exe created
	to only have .prt as the extension.
	
6.	That's it. Now just hit "Go!" when you want to compile. You won't have to
	enter that stuff in again unless you are setting up a new installation of
	hammer.

