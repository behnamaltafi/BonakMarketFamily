cd ..
docker build -t ghcr.io/BonakMarket/BonakMarket-linux -f NoBuild.Dockerfile .
echo 'Press enter to exit...'; read dummy;