# http://serialseb.com/blog/2011/03/15/opening-a-solution-from-the-command-line-in-powershell/
# What this little script does is find the first solution file under your current path and open it in Visual Studio.
ls -in *.sln -r | select -first 1 | %{ ii $_.FullName }