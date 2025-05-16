build:
	dotnet publish WatchAlong.csproj -o out
	cp LICENSE COPYRIGHT NOTICE README.md out/

format:
	dotnet format WatchAlong.csproj --verbosity diag --severity info --include-generated
