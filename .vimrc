set shiftwidth=4
set tabstop=4
set expandtab
set number

execute pathogen#infect()

syntax on
filetype plugin indent on

let mapleader="\\"
nnoremap <leader>ev :vs $MYVIMRC<cr>
nnoremap <leader>sv :source $MYVIMRC<cr>

nnoremap <leader>ln :call fsharpbinding#python#FsiSendLine()<cr>
vnoremap <leader>sel :call fsharpbinding#python#FsiSendSel()<cr>
