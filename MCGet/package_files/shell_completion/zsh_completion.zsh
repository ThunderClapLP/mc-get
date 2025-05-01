#compdef mc-get

_mc-get() {
  local -a opts commands
  local cur prev

  opts=(--help -p --platform -s --silent -m --mc-path --path --mc-version --server --version)
  commands=(install search list remove)

  cur=${words[CURRENT]}
  prev=${words[CURRENT-1]}

  case "$prev" in
    --help | --version | -v)
      return
      ;;
    --path | -m | --mc-path)
      _files -/
      return
      ;;
    -p | --platform)
      compadd mr cf modrinth curseforge
      return
      ;;
    list)
      compadd installs mods
      return
      ;;
    remove)
      compadd installation mod
      return
      ;;
  esac

  local has_command
  local i
  has_command=0
  for (( i=2; i < CURRENT; i++ )); do
    for command in "${commands[@]}"; do
      if [[ "${words[i]}" == "$command" ]]; then
        has_command=1
        break 2
      fi
    done
  done

  if [[ "$cur" == -* && $has_command -eq 0 ]]; then
    compadd -- "${opts[@]}"
    return 0
  elif [[ $has_command -eq 0 ]]; then
    compadd -- "${commands[@]}"
    return 0
  fi
}

compdef _mc-get mc-get