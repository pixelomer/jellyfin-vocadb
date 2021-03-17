bin/Release/net5.0/publish/VocaDB.dll: $(wildcard *.cs)
	dotnet publish --configuration Release

install: bin/Release/net5.0/publish/VocaDB.dll
	@mkdir -p /mnt/data/Jellyfin/config/plugins/VocaDB
	cp bin/Release/net5.0/publish/VocaDB.dll /mnt/data/Jellyfin/config/plugins/VocaDB/
	sudo docker restart jellyfin