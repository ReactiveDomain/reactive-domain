#!/bin/bash
# Script to verify links in markdown documentation

DOCS_DIR="/Users/leopoldodonnell/dev/reactive-domain/docs"
BROKEN_LINKS_FILE="$DOCS_DIR/broken-links.txt"

# Clear the broken links file
> "$BROKEN_LINKS_FILE"

# Function to extract links from markdown files
extract_links() {
    local file="$1"
    grep -o '\[[^]]*\]([^)]*)' "$file" | sed 's/\[[^]]*\](\([^)]*\))/\1/g'
}

# Function to check if a link is valid
check_link() {
    local file="$1"
    local link="$2"
    local base_dir=$(dirname "$file")
    
    # Skip external links and anchors
    if [[ "$link" == http* || "$link" == "#"* ]]; then
        return 0
    fi
    
    # Handle relative links
    if [[ "$link" != /* ]]; then
        link="$base_dir/$link"
    fi
    
    # Normalize the path
    link=$(echo "$link" | sed 's|/\./|/|g' | sed 's|/[^/]*/\.\./|/|g')
    
    # Check if the file exists
    if [[ ! -f "$link" ]]; then
        echo "Broken link in $file: $link" >> "$BROKEN_LINKS_FILE"
        return 1
    fi
    
    return 0
}

# Find all markdown files and check their links
find "$DOCS_DIR" -name "*.md" -type f | while read -r file; do
    echo "Checking links in $file"
    extract_links "$file" | while read -r link; do
        check_link "$file" "$link"
    done
done

# Report results
if [[ -s "$BROKEN_LINKS_FILE" ]]; then
    echo "Found broken links:"
    cat "$BROKEN_LINKS_FILE"
else
    echo "No broken links found."
fi
