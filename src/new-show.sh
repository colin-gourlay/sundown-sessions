#!/bin/bash

# Usage:
# ./new-show.sh '21' \
# '5th June 2024' \
# 'the-big-now' \
# 'The Big Now' \
# 'IST IST' \
# 'Becky Becky' \
# 'Nick Cave & The Bad Seeds' \
# 'The Filthy Tongues' \
# '2024-06-05T22:00:00Z'

SHOW_NUMBER=$1
BROADCAST_DATE=$2
FEATURED_GUEST_SLUG=$3
FEATURED_GUEST=$4
FEATURED_ARTIST_1=$5
FEATURED_ARTIST_2=$6
FEATURED_ARTIST_3=$7
FEATURED_ARTIST_4=$8
SHOW_DATE_TIME=$9

mkdir "./content/shows/$SHOW_NUMBER/"

cat << EOF > "./content/shows/$SHOW_NUMBER/additional-resources.md"
### Additional Resources

EOF

cat << EOF > ./archetypes/default.md
---
title: 'Show #$SHOW_NUMBER: Broadcast $BROADCAST_DATE'
slug: 'featuring-$FEATURED_GUEST_SLUG'
description: 'featuring $FEATURED_GUEST'
summary: 'Live on K107, THE SUNDOWN SESSIONS returns with...

          - $FEATURED_ARTIST_1

          - $FEATURED_ARTIST_2

          - $FEATURED_ARTIST_3

          - $FEATURED_ARTIST_4

          - and much, much more...
'
keywords: 
  - '$FEATURED_GUEST'
  - '$FEATURED_ARTIST_1'
  - '$FEATURED_ARTIST_2'
  - '$FEATURED_ARTIST_3'
  - '$FEATURED_ARTIST_4'
featured_image: '$SHOW_NUMBER-show-logo.jpeg'
read_more_copy: Show notes...
show_reading_time: true
date: '$SHOW_DATE_TIME'
draft: true
---
{{< include_content "/shows/$SHOW_NUMBER/playlist" >}}

---

{{< include_content "/shows/$SHOW_NUMBER/show-notes" >}}
{{< include_content "/shows/$SHOW_NUMBER/additional-resources" >}}
{{< include_content "/shows/$SHOW_NUMBER/track-info" >}}

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/playlist.md"
## Playlist

1. 

- ADVERTISING BREAK

1. David Latto & The Lang Spoons - Geordie Munro

- ADVERTISING BREAK

1. 

- NEWS

1. 

- ADVERTISING BREAK

1. 

- ADVERTISING BREAK

1. 
2. The Filthy Tongues - Nae Tongues

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/show-notes.md"
## Show Notes

EOF

cat << EOF > "./content/shows/$SHOW_NUMBER/track-info.md"
### Track Info

| Artist                        | Track                                | Duration | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
|-------------------------------|--------------------------------------|----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

EOF

hugo new "shows/$SHOW_NUMBER/index.md"
