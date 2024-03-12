+++
title: 'Show #{{SHOW_NUMBER}}: Broadcast {{BROADCAST_DATE}}'
slug: 'featuring-{{FEATURED_GUEST_SLUG}}'
description: 'featuring {{FEATURED_GUEST}}'
summary: 'Live from K107, THE SUNDOWN SESSIONS returns with...
          
          - {{FEATURED_ARTIST_1}}

          - {{FEATURED_ARTIST_2}}

          - {{FEATURED_ARTIST_3}}

          - {{FEATURED_ARTIST_4}}

          - and much, much more...
'
featured_image: '{{SHOW_NUMBER}}-show-logo.jpeg'
read_more_copy: 'Show notes...'
show_reading_time: true
date: '{{BROADCAST_DATE}}T{{SHOW_TIME}}'
draft: true
+++
{{< include_content "/shows/{{SHOW_NUMBER}}/playlist" >}}

---


{{< include_content "/shows/{{SHOW_NUMBER}}/show-notes" >}}
{{< include_content "/shows/{{SHOW_NUMBER}}/additional-resources" >}}
{{< include_content "/shows/{{SHOW_NUMBER}}/track-info" >}}
