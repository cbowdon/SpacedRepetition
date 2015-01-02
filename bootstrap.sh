#!/bin/bash

# Add the Xamarin Mono package repository
apt-key adv --keyserver keyserver.ubuntu.com --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list

apt-get update
apt-get install -y vim git mono-complete fsharp

# NuGet
curl -LSso /usr/bin/NuGet.exe http://build.nuget.org/NuGet.exe
echo '
alias nuget="mono /usr/bin/NuGet.exe"' >> /home/vagrant/.bashrc

# Vim
mkdir -p /home/vagrant/.vim/autoload /home/vagrant/.vim/bundle
ln -sf /vagrant/.vimrc /home/vagrant/.vimrc

# Vim: pathogen
curl -LSso /home/vagrant/.vim/autoload/pathogen.vim https://tpo.pe/pathogen.vim

# Vim: syntastic
if [[ ! -d /home/vagrant/.vim/bundle/syntastic ]]; then
    git clone https://github.com/scrooloose/syntastic.git /home/vagrant/.vim/bundle/syntastic
fi

# Vim: F# binding
if [[ ! -d /home/vagrant/fsharpbinding ]]; then
    git clone https://github.com/fsharp/fsharpbinding.git /home/vagrant
fi
cd /home/vagrant/fsharpbinding/vim
make install

git config --global user.name "cbowdon"
git config --global user.email "cbowdon@users.noreply.github.com"
