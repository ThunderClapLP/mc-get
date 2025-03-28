_mc-get()
{
  local cur prev words cword
  _init_completion || return
  opts="--help -s --silent -mr --modrinth -cf --curseforge -m --path -mc --server -v --version"
  commands="install search list remove"
  
  case "$prev" in
    --help | --version | -v)
    return
    ;;
    --path | -m)
    COMPREPLY=( $(compgen -d -- ${cur}) )
    return
    ;;
    list)
    COMPREPLY=( $(compgen -W "installs mods" -- ${cur}) )
    return
    ;;
    remove)
    COMPREPLY=( $(compgen -W "installation mod" -- ${cur}) )
    return
    ;;
  esac
  
  local has_command
  local i
  has_command=0
  for ((i = 1; i < cword; i++)); do
    if [[ ${commands} == *${words[i]}* ]]; then
      has_command=1
      break
    fi
  done
  
  if [[ "$cur" == -* ]] && [[ "$has_command" == 0 ]] ; then
    COMPREPLY=( $(compgen -W "${opts}" -- ${cur}) )
    return 0
  elif [[ ${has_command} == 0 ]] ; then
    COMPREPLY=( $(compgen -W "${commands}" -- ${cur}) )
    return 0
  fi
}

complete -F _mc-get mc-get

# ex: filetype=sh
