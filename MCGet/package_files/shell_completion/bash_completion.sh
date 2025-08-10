_mc-get()
{
  local cur prev words cword
  _init_completion || return
  opts="--help -p --platform -s --silent -m --mc-path --path --mc-version --server --version"
  commands="install search list remove"
  
  case "$prev" in
    --help | --version)
    return
    ;;
    --path | -m | --mc-path)
    COMPREPLY=( $(compgen -d -- ${cur}) )
    return
    ;;
    -p | --platform)
    COMPREPLY=( $(compgen -W "mr cf modrinth curseforge" -- ${cur}) )
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
